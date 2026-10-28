namespace MossAgent.Domain.Plugins;

/// <summary>Snapshot of one skill component activated for a task dispatch: the identity of the
/// plugin and component plus the exact file content that entered the system prompt. Persisted per
/// task so a dispatched run is insulated from later plugin state changes by construction.
/// <see cref="TrustRevision"/> records the plugin's trust revision at dispatch — the trust
/// generation the injection ran under.</summary>
public sealed record TaskSkillActivation(
    Guid Id,
    Guid TaskId,
    Guid PluginId,
    string PluginName,
    string PluginVersion,
    string ContentHash,
    string ComponentPath,
    string SkillText,
    DateTimeOffset ActivatedAt,
    long TrustRevision);
