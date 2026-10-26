using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.SourceControl;

internal static class SourceReadFailure
{
    public static ToolResult Create(string code) => ToolResult.Failure(ToolExecutionText.Get(code switch
    {
        "source_revision_changed" => "SourceRevisionChanged",
        "source_scope_denied" => "SourceScopeDenied",
        "source_output_limit" => "SourceOutputLimit",
        "source_write_rejected" => "SourceWriteRejected",
        "source_write_unconfirmed" => "SourceWriteUnconfirmed",
        "source_write_unknown" => "SourceWriteUnknown",
        _ => "SourceReadFailed"
    }), code);
}
