namespace MossAgent.Domain.Plugins;

/// <summary>Result of a best-effort startup reconciliation: staging directories left by
/// interrupted installs were removed; final directories without a database row are only
/// reported as orphaned paths, never deleted automatically.</summary>
public sealed record PluginInstallReconciliation(int RemovedStaging, IReadOnlyList<string> OrphanedFinals);
