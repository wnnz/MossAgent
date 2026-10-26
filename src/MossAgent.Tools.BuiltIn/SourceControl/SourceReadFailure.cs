using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

internal static class SourceReadFailure
{
    public static ToolResult Create(string code) => Create(code, "comment");

    public static ToolResult CreateReview(string code) => Create(code, "review");

    public static ToolResult CreateClose(string code) => Create(code, "close");

    public static ToolResult CreateMerge(string code) => Create(code, "merge");

    private static ToolResult Create(string code, string writeKind) => ToolResult.Failure(ToolExecutionText.Get(code switch
    {
        "source_revision_changed" => "SourceRevisionChanged",
        "source_scope_denied" => "SourceScopeDenied",
        "source_output_limit" => "SourceOutputLimit",
        "source_write_rejected" => WriteKey(writeKind, "Rejected"),
        "source_write_unconfirmed" => WriteKey(writeKind, "Unconfirmed"),
        "source_write_unknown" => WriteKey(writeKind, "Unknown"),
        _ => "SourceReadFailed"
    }), code);

    private static string WriteKey(string writeKind, string outcome) => writeKind switch
    {
        "review" => "SourceReviewWrite" + outcome,
        "close" => "SourceClose" + outcome,
        "merge" => "SourceMerge" + outcome,
        _ => "SourceWrite" + outcome
    };
}
