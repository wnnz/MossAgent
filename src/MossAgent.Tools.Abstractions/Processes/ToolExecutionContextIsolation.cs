using System.Security.Cryptography;
using System.Text;

namespace MossAgent.Tools.Abstractions.Processes;

/// <summary>
/// Derives the per-task <see cref="ProcessIsolationSpec"/> from a tool execution context.
/// The sandbox key is stable per task and authorized-root set so grants can be shared.
/// </summary>
public static class ToolExecutionContextIsolation
{
    public static ProcessIsolationSpec CreateProcessIsolation(this ToolExecutionContext context)
    {
        var writableRoots = context.AuthorizedRoots
            .Select(root => Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)))
            .OrderBy(root => root, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var key = $"mossagent-{context.TaskId:N}-{RootsHash(writableRoots)}";
        var privateDirectory = context.ArtifactDirectory is { Length: > 0 } artifact
            ? Path.Combine(Path.GetFullPath(artifact), "sandbox-tmp")
            : Path.Combine(Path.GetTempPath(), key);
        return new ProcessIsolationSpec(key, writableRoots, [], privateDirectory);
    }

    private static string RootsHash(IReadOnlyList<string> normalizedRoots)
    {
        var joined = string.Join(';', normalizedRoots.Select(root => root.ToLowerInvariant()));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        var builder = new StringBuilder(8);
        foreach (var value in hash[..4])
            builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return builder.ToString();
    }
}
