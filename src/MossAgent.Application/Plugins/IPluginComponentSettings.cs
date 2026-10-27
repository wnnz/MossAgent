namespace MossAgent.Application.Plugins;

/// <summary>Persists explicit per-component enable choices for installed plugins. Only skill
/// components are selectable today, so the contract is deliberately narrow. A stored row is the
/// last explicit choice for one component; the absence of a row means the component is enabled
/// by default. Rows belong to the plugin row id — uninstall cascades them away and a reinstall
/// under a new id starts from defaults again.</summary>
public interface IPluginComponentSettings
{
    /// <summary>Returns the effective enable state for one skill component: the stored choice
    /// when present, otherwise the default-enabled absence.</summary>
    Task<bool> IsSkillEnabledAsync(Guid pluginId, string componentPath, CancellationToken cancellationToken);

    /// <summary>Records the explicit choice for one skill component. Storage failures propagate;
    /// writing against a plugin row that no longer exists fails on the foreign key.</summary>
    Task SetSkillEnabledAsync(Guid pluginId, string componentPath, bool enabled, CancellationToken cancellationToken);

    /// <summary>Returns every stored choice for one plugin as component-path → enabled, or an
    /// empty map when the plugin has no explicit rows.</summary>
    Task<IReadOnlyDictionary<string, bool>> ListOverridesAsync(Guid pluginId, CancellationToken cancellationToken);
}
