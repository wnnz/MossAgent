using System.Text;
using MossAgent.App.Resources;
using MossAgent.Domain.Plugins;

namespace MossAgent.App.ViewModels;

/// <summary>Projects one persisted <see cref="TaskSkillActivation"/> into the audit pane. The
/// row shows which plugin version and component injected instructions into this task's prompt;
/// the detail is a bounded excerpt of the exact stored skill text so the snapshot stays
/// auditable without replaying it.</summary>
public sealed class AuditSkillActivationViewModel(TaskSkillActivation record)
{
    private const int DetailLimit = 500;

    public TaskSkillActivation Record { get; } = record;
    public string ProfileName => $"{LimitName(Record.PluginName)} v{LimitName(Record.PluginVersion)}";
    public string OutcomeKind => "success";
    public string OutcomeLabel => InspectorText.Get("AuditSkillInjected");
    public string Metadata => InspectorText.Format("AuditSkillActivationMetadata",
        Record.ComponentPath, Record.ContentHash[..Math.Min(8, Record.ContentHash.Length)],
        Record.ActivatedAt.ToLocalTime());
    public string Detail => Limit(Record.SkillText, DetailLimit);

    private static string LimitName(string value) => Limit(value, 160);

    private static string Limit(string value, int maximum)
    {
        var result = new StringBuilder();
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsControl(rune) && rune.Value is not ('\n' or '\r' or '\t')) continue;
            if (result.Length + rune.Utf16SequenceLength > maximum) break;
            result.Append(rune.ToString());
        }
        return result.ToString();
    }
}
