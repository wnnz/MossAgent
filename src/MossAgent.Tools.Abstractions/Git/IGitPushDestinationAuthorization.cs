namespace MossAgent.Tools.Abstractions.Git;

public interface IGitPushDestinationAuthorization
{
    Task<ToolGitRepository> AuthorizeAsync(string directory, string targetBranch,
        ToolExecutionContext context, CancellationToken cancellationToken);
}
