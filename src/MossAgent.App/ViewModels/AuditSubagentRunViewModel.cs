using System.Text;
using System.Text.Json;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;

namespace MossAgent.App.ViewModels;

/// <summary>
/// Projects one journaled <see cref="SubagentRunRecord"/> into the audit pane. The five
/// journal states map onto the shared outcome kinds so terminal runs stay viewable.
/// The bounded snapshot is parsed defensively; a malformed snapshot degrades to the
/// profile name and status instead of surfacing a parse failure to the UI thread.
/// </summary>
public sealed class AuditSubagentRunViewModel
{
    private readonly string? _snapshotSummary;

    public AuditSubagentRunViewModel(SubagentRunRecord record)
    {
        Record = record;
        _snapshotSummary = ParseSnapshot(record.SnapshotJson);
    }

    public SubagentRunRecord Record { get; }
    public string ProfileName => LimitName(Record.ProfileName);
    public string OutcomeKind => Record.Status switch
    {
        SubagentRunStatus.Running => "running",
        SubagentRunStatus.Completed => "success",
        SubagentRunStatus.Cancelled => "cancelled",
        SubagentRunStatus.Interrupted => "interrupted",
        _ => "failed"
    };
    public string OutcomeLabel => InspectorText.Get(OutcomeKind switch
    {
        "success" => "Succeeded",
        "running" => "AuditSubagentRunning",
        "interrupted" => "AuditInterrupted",
        "cancelled" => "AuditCancelled",
        _ => "Failed"
    });
    public string Detail
    {
        get
        {
            if (_snapshotSummary is null) return $"{ProfileName} · {OutcomeLabel}";
            var hint = OutcomeKind switch
            {
                "running" => InspectorText.Get("AuditSubagentRunningHint"),
                "interrupted" => InspectorText.Get("AuditSubagentInterruptedHint"),
                "cancelled" => InspectorText.Get("AuditSubagentCancelledHint"),
                "failed" => InspectorText.Get("AuditSubagentFailedHint"),
                _ => string.Empty
            };
            return hint.Length == 0 ? _snapshotSummary : $"{hint}\n{_snapshotSummary}";
        }
    }
    public string DurationLabel => Record.Status == SubagentRunStatus.Running
        ? InspectorText.Get("Pending")
        : Record.UpdatedAt >= Record.CreatedAt
            ? InspectorText.Format("AuditDuration", Math.Ceiling((Record.UpdatedAt - Record.CreatedAt).TotalMilliseconds))
            : InspectorText.Get("AuditNotFinished");
    public string Metadata => InspectorText.Format("AuditMetadata", Record.CreatedAt.ToLocalTime(), DurationLabel);

    private static string? ParseSnapshot(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            var share = root.TryGetProperty("shareContext", out var shared) && shared.ValueKind == JsonValueKind.True;
            var effort = LimitName(ReadText(root, "reasoningEffort"));
            return InspectorText.Format("AuditSubagentSnapshot",
                LimitName(ReadText(root, "providerProtocol")),
                LimitName(ReadText(root, "modelId")),
                effort.Length == 0 ? "—" : effort,
                InspectorText.Get(share ? "AuditSubagentShareOn" : "AuditSubagentShareOff"),
                LimitName(ReadText(root, "taskPreview")));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string ReadText(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

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
