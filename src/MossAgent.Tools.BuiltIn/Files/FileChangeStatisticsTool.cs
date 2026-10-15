using System.Globalization;
using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileChangeStatisticsTool(AuthorizedPathResolver paths, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.change_statistics", FileToolText.Get("ChangeStatisticsDescription"),
        FileChangeRequest.InputSchema, ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process)
    {
        IsModelVisible = false
    };

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => FileChangeRequest.Validate(request, context, paths);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var change = FileChangeRequest.Validate(request, context, paths);
        var (before, after) = await new FileChangeSnapshotReader().ReadChangeAsync(change, context, cancellationToken);
        var output = await new FileChangeDiffRunner(git).RunAsync(before, after, true, context, cancellationToken);
        var statistics = Parse(output);
        paths.Resolve(change.Path, context);
        cancellationToken.ThrowIfCancellationRequested();
        return new(true, FileToolText.Get("ChangeStatisticsSummary"), JsonSerializer.Serialize(statistics));
    }

    private static ToolFileChangeStatistics Parse(string output)
    {
        if (string.IsNullOrWhiteSpace(output)) return new(0, 0);
        var fields = output.TrimEnd('\r', '\n').Split('\t');
        if (fields.Length != 3 || !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out var added)
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out var removed)
            || added < 0 || removed < 0 || fields[2].Contains('\n') || fields[2].Contains('\r'))
            throw new FileEditException("snapshot_review_failed", "ReviewChangeFailed");
        return new(added, removed);
    }
}
