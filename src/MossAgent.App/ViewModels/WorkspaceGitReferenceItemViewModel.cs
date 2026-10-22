using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceGitReferenceItemViewModel
{
    public WorkspaceGitReferenceItemViewModel(GitReferenceSuggestion suggestion,
        Func<WorkspaceGitReferenceItemViewModel, Task> select)
    {
        Suggestion = suggestion;
        SelectCommand = new AsyncRelayCommand(() => select(this));
    }

    public GitReferenceSuggestion Suggestion { get; }
    public IAsyncRelayCommand SelectCommand { get; }
    public string Name => Suggestion.Name;
    public string KindLabel => WorkspaceText.Get(Suggestion.Kind == GitReferenceKind.Branch
        ? "GitReferenceBranch" : "GitReferenceTag");
    public string CommitLabel => Suggestion.Commit.Length <= 8 ? Suggestion.Commit : Suggestion.Commit[..8];
    public string AutomationLabel => $"{KindLabel} {Name} {CommitLabel}";
}
