using MossAgent.Application.Git;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceWorktreeReferenceItemViewModel(GitWorktreeReference reference)
{
    public GitWorktreeReference Reference { get; } = reference;

    public string Name => Reference.Name;

    public string KindLabel => WorkspaceText.Get(Reference.Kind switch
    {
        GitWorktreeReferenceKind.Branch => "TaskEnvironmentReferenceBranch",
        GitWorktreeReferenceKind.Remote => "TaskEnvironmentReferenceRemote",
        _ => "TaskEnvironmentReferenceTag",
    });

    public string Display => $"{Reference.Name} · {KindLabel}";

    public string Preview => Reference.Subject.Length == 0
        ? Reference.Commit[..8]
        : $"{Reference.Commit[..8]}  {Reference.Subject}";
}
