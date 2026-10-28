namespace MossAgent.Domain.Plugins;

/// <summary>One per-task explicit enable choice for a plugin skill component. The row beats the
/// plugin-wide choice stored in component settings and the default-enabled absence; it belongs to
/// the task, so uninstalling the plugin leaves the row as inert history.</summary>
public sealed record TaskSkillOverride(Guid TaskId, Guid PluginId, string ComponentPath, bool Enabled);
