namespace MossAgent.Infrastructure.Git;

internal sealed record GitWorktreeRepository(string Root, string CommonDirectory, string GitDirectory, string? Head)
{
    public IReadOnlyList<string> Command(params string[] arguments) =>
        [$"--git-dir={CommonDirectory}", $"--work-tree={Root}", .. arguments];
}
