namespace MossAgent.Tools.Abstractions.Git;

public sealed record ToolGitRepository(string Root, string GitDirectory, string CommonDirectory)
{
    public IReadOnlyList<string> Command(params string[] arguments) =>
        new[] { $"--git-dir={GitDirectory}", $"--work-tree={Root}", "--literal-pathspecs" }.Concat(arguments).ToArray();
}
