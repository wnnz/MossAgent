namespace MossAgent.Tools.Abstractions.Processes;

/// <summary>
/// Immutable description of the filesystem sandbox a task process must run in.
/// Validated at construction so a malformed spec never reaches the launcher.
/// </summary>
public sealed record ProcessIsolationSpec
{
    public ProcessIsolationSpec(string sandboxKey, IReadOnlyList<string> writableRoots,
        IReadOnlyList<string> readOnlyPaths, string? privateDirectory = null, bool allowOutboundNetwork = true)
    {
        ArgumentException.ThrowIfNullOrEmpty(sandboxKey);
        if (sandboxKey.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')))
            throw new ArgumentException("沙箱标识只能包含字母、数字、点、下划线和连字符。", nameof(sandboxKey));
        if (writableRoots is not { Count: > 0 })
            throw new ArgumentException("沙箱至少需要一个可写授权根。", nameof(writableRoots));
        ArgumentNullException.ThrowIfNull(readOnlyPaths);
        foreach (var path in writableRoots.Concat(readOnlyPaths))
            RequireAbsolute(path);
        if (privateDirectory is not null)
            RequireAbsolute(privateDirectory);
        SandboxKey = sandboxKey;
        WritableRoots = writableRoots;
        ReadOnlyPaths = readOnlyPaths;
        PrivateDirectory = privateDirectory;
        AllowOutboundNetwork = allowOutboundNetwork;
    }

    public string SandboxKey { get; init; }
    public IReadOnlyList<string> WritableRoots { get; init; }
    public IReadOnlyList<string> ReadOnlyPaths { get; init; }
    public string? PrivateDirectory { get; init; }
    public bool AllowOutboundNetwork { get; init; }

    private static void RequireAbsolute(string path)
    {
        if (!Path.IsPathFullyQualified(path))
            throw new ArgumentException($"沙箱路径必须是绝对路径：{path}");
    }
}
