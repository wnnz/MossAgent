using System.Text;
using MossAgent.App.Resources;
using MossAgent.Tools.Abstractions;

namespace MossAgent.App.ViewModels;

public sealed class AuditExecutionViewModel(ToolExecutionAuditRecord record)
{
    public ToolExecutionAuditRecord Record { get; } = record;
    public string ToolName => LimitName(Record.ToolName);
    public string OutcomeKind => Record.CompletedAt is null && Record.InterruptedAt is not null ? "interrupted"
        : Record.CompletedAt is null || Record.IsSuccess is null ? "pending"
        : Record.IsSuccess is true ? "success"
        : Record.ErrorCode == "tool_cancelled" ? "cancelled"
        : Record.ApprovalGranted is false ? "denied" : "failed";
    public string OutcomeLabel => InspectorText.Get(OutcomeKind switch
    {
        "success" => "Succeeded",
        "pending" => "Pending",
        "interrupted" => "AuditInterrupted",
        "cancelled" => "AuditCancelled",
        "denied" => "AuditDenied",
        _ => "Failed"
    });
    public string Detail => OutcomeKind switch
    {
        "success" or "pending" => string.Empty,
        "interrupted" => InspectorText.Get("AuditInterruptedHint"),
        "cancelled" => InspectorText.Get("AuditCancelledHint"),
        "denied" => InspectorText.Get(Record.ErrorCode == "path_denied" ? "AuditPathHint" : "AuditDeniedHint"),
        _ => InspectorText.Get(Record.ErrorCode switch
        {
            "path_denied" => "AuditPathHint",
            "tool_timeout" => "AuditTimeoutHint",
            "tool_schema_invalid" or "invalid_arguments" => "AuditArgumentsHint",
            "subagent_reasoning_unavailable" => "AuditSubagentReasoningHint",
            _ => "AuditFailureHint"
        })
    };
    public bool HasDetail => Detail.Length > 0;
    public string DurationLabel => Record.CompletedAt is { } completed && completed >= Record.StartedAt
        ? InspectorText.Format("AuditDuration", Math.Ceiling((completed - Record.StartedAt).TotalMilliseconds))
        : InspectorText.Get(Record.InterruptedAt is null ? "AuditNotFinished" : "AuditUnknownDuration");
    public string Metadata => InspectorText.Format("AuditMetadata", Record.StartedAt.ToLocalTime(), DurationLabel);

    private static string LimitName(string value)
    {
        var result = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsControl(rune)) continue;
            if (result.Length + rune.Utf16SequenceLength > 160) break;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }
}
