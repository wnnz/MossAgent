using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

internal static class SourceReadFailure
{
    public static ToolResult Create(string code) => Create(code, review: false);

    public static ToolResult CreateReview(string code) => Create(code, review: true);

    private static ToolResult Create(string code, bool review) => ToolResult.Failure(ToolExecutionText.Get(code switch
    {
        "source_revision_changed" => "SourceRevisionChanged",
        "source_scope_denied" => "SourceScopeDenied",
        "source_output_limit" => "SourceOutputLimit",
        "source_write_rejected" => review ? "SourceReviewWriteRejected" : "SourceWriteRejected",
        "source_write_unconfirmed" => review ? "SourceReviewWriteUnconfirmed" : "SourceWriteUnconfirmed",
        "source_write_unknown" => review ? "SourceReviewWriteUnknown" : "SourceWriteUnknown",
        _ => "SourceReadFailed"
    }), code);
}
