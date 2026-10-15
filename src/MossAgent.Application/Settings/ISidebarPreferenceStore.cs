namespace MossAgent.Application.Settings;

public interface ISidebarPreferenceStore
{
    Task<SidebarPreference> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(SidebarPreference preference, CancellationToken cancellationToken);
}
