using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MossAgent.App.ViewModels;

internal sealed class WorkspaceProjectPickerController : ObservableObject
{
    private readonly WorkspaceCatalogViewModel _catalog;
    private string _query = string.Empty;

    public WorkspaceProjectPickerController(WorkspaceCatalogViewModel catalog) => _catalog = catalog;

    public ObservableCollection<WorkspaceProjectNavigationViewModel> Projects { get; } = [];

    public string Query
    {
        get => _query;
        set
        {
            if (SetProperty(ref _query, value)) Refresh();
        }
    }

    public bool HasMatches => Projects.Count > 0;

    public void Open()
    {
        Query = string.Empty;
        Refresh();
    }

    public void Refresh()
    {
        var query = Query.Trim();
        Projects.ReplaceWith(_catalog.ProjectNavigation.Where(group =>
            group.Project.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)));
        OnPropertyChanged(nameof(HasMatches));
    }
}
