using MossAgent.Application.Settings;

namespace MossAgent.App.Tests;

internal sealed class TestSidebarPreferenceStore : ISidebarPreferenceStore
{
    public SidebarPreference Saved { get; private set; } = new(SidebarProjectSortOrder.UpdatedAt, []);
    public int Loads { get; private set; }
    public int Saves { get; private set; }
    public Func<CancellationToken, Task<SidebarPreference>>? LoadHandler { get; set; }
    public Func<SidebarPreference, CancellationToken, Task>? SaveHandler { get; set; }

    public Task<SidebarPreference> LoadAsync(CancellationToken cancellationToken)
    {
        Loads++;
        return LoadHandler?.Invoke(cancellationToken) ?? Task.FromResult(Saved);
    }

    public async Task SaveAsync(SidebarPreference preference, CancellationToken cancellationToken)
    {
        Saves++;
        if (SaveHandler is not null) await SaveHandler(preference, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        Saved = preference;
    }
}
