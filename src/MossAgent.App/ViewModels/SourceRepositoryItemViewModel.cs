using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record SourceRepositoryItemViewModel(SourceRepositoryScope Scope, string AccountName)
{
    public string Name => $"{AccountName} · {Scope.Owner}/{Scope.Repository}";
    public bool CanWrite => Scope.CanWrite;
    public string AccessLabel => Scope.CanWrite ? PullRequestText.AccessReadWrite : PullRequestText.AccessReadOnly;
}
