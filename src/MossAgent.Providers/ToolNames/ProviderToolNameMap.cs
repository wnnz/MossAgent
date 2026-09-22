using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Providers.ToolNames;

internal sealed class ProviderToolNameMap
{
    private const int MaximumWireNameLength = 64;
    private readonly Dictionary<string, string> _toWire = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _fromWire = new(StringComparer.Ordinal);
    private readonly HashSet<string> _reserved;

    public ProviderToolNameMap(IEnumerable<ToolDescriptor> tools)
    {
        var names = tools.Select(static tool => tool.Name).Distinct(StringComparer.Ordinal).ToArray();
        _reserved = names.Where(IsWireSafe).ToHashSet(StringComparer.Ordinal);
        foreach (var name in names)
        {
            Add(name);
        }
    }

    public string Encode(string name)
    {
        if (_toWire.TryGetValue(name, out var wireName))
        {
            return wireName;
        }

        return Add(name);
    }

    public string Decode(string wireName) =>
        _fromWire.TryGetValue(wireName, out var name) ? name : wireName;

    private string Add(string name)
    {
        var wireName = IsWireSafe(name) && !_fromWire.ContainsKey(name)
            ? name
            : CreateEncodedName(name);
        _toWire[name] = wireName;
        _fromWire[wireName] = name;
        return wireName;
    }

    private string CreateEncodedName(string name)
    {
        var readable = new string(name.Select(static character =>
            IsWireCharacter(character) ? character : '_').ToArray()).Trim('_');
        if (readable.Length == 0)
        {
            readable = "tool";
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..12].ToLowerInvariant();
        var suffix = $"__{hash}";
        readable = readable[..Math.Min(readable.Length, MaximumWireNameLength - suffix.Length)];
        var candidate = readable + suffix;
        var collision = 1;
        while (_reserved.Contains(candidate) || _fromWire.ContainsKey(candidate))
        {
            var collisionSuffix = $"_{collision++}";
            var prefixLength = MaximumWireNameLength - suffix.Length - collisionSuffix.Length;
            candidate = readable[..Math.Min(readable.Length, prefixLength)] + suffix + collisionSuffix;
        }

        return candidate;
    }

    private static bool IsWireSafe(string name) =>
        name.Length is > 0 and <= MaximumWireNameLength && name.All(IsWireCharacter);

    private static bool IsWireCharacter(char character) =>
        character is >= 'a' and <= 'z'
            or >= 'A' and <= 'Z'
            or >= '0' and <= '9'
            or '_' or '-';
}
