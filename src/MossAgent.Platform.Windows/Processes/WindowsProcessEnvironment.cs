using System.Collections;
using System.Runtime.InteropServices;

namespace MossAgent.Platform.Windows.Processes;

internal sealed class WindowsProcessEnvironment : IDisposable
{
    private readonly char[]? _characters;
    private GCHandle _pinned;

    public WindowsProcessEnvironment(IReadOnlyDictionary<string, string?>? overrides)
    {
        if (overrides is null) return;
        var variables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            variables[(string)entry.Key] = (string)entry.Value!;
        foreach (var pair in overrides)
        {
            if (string.IsNullOrEmpty(pair.Key) || pair.Key.Contains('=') || pair.Key.Contains('\0')
                || pair.Value?.Contains('\0') == true)
                throw new ArgumentException("Invalid process environment name or value.", nameof(overrides));
            if (pair.Value is null) variables.Remove(pair.Key);
            else variables[pair.Key] = pair.Value;
        }

        var block = string.Join('\0', variables.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => $"{pair.Key}={pair.Value}")) + "\0\0";
        if (block.Length > 1_048_576)
            throw new ArgumentException("Process environment exceeds its size limit.", nameof(overrides));
        _characters = block.ToCharArray();
        _pinned = GCHandle.Alloc(_characters, GCHandleType.Pinned);
    }

    public nint Pointer => _pinned.IsAllocated ? _pinned.AddrOfPinnedObject() : 0;

    public void Dispose()
    {
        if (!_pinned.IsAllocated) return;
        Array.Clear(_characters!);
        _pinned.Free();
    }
}
