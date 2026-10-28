using MossAgent.Domain.Plugins;

namespace MossAgent.Application.Plugins;

/// <summary>Persists per-task explicit enable choices for plugin skill components — the layer
/// that lets one conversation deviate from the plugin-wide choice kept in
/// <see cref="IPluginComponentSettings"/>. A stored row is the last explicit choice for one
/// component of one task; the absence of a row means the task defers to the global layer.
/// Rows belong to the task and cascade with it. The plugin id is deliberately not a foreign
/// key: uninstall/reinstall swaps the plugin row id, and stale rows stay behind as inert
/// history rather than being deleted or revived.</summary>
public interface ITaskSkillOverrides
{
    /// <summary>Returns every stored choice for the task across all plugins, or an empty list
    /// when the task carries no per-conversation choices.</summary>
    Task<IReadOnlyList<TaskSkillOverride>> ListForTaskAsync(Guid taskId, CancellationToken cancellationToken);

    /// <summary>Upserts the explicit choice for one skill component of one task. Storage
    /// failures propagate; writing against a task row that no longer exists fails on the
    /// foreign key.</summary>
    Task SetSkillEnabledAsync(Guid taskId, Guid pluginId, string componentPath, bool enabled,
        CancellationToken cancellationToken);

    /// <summary>Deletes every override row for the task — the "reset to global" path that
    /// returns the conversation to plugin-wide choices and defaults.</summary>
    Task ClearForTaskAsync(Guid taskId, CancellationToken cancellationToken);
}
