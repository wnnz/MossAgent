namespace MossAgent.Tools.Abstractions;

public sealed record ToolResult(
    bool IsSuccess,
    string Summary,
    string? Content = null,
    IReadOnlyList<ToolArtifact>? Artifacts = null,
    string? ErrorCode = null)
{
    public static ToolResult Success(string summary, string? content = null) =>
        new(true, summary, content);

    public static ToolResult Failure(string summary, string errorCode) =>
        new(false, summary, ErrorCode: errorCode);
}

