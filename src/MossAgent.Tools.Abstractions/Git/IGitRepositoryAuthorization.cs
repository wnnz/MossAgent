namespace MossAgent.Tools.Abstractions.Git;

public interface IGitRepositoryAuthorization
{
    Task<ToolGitRepository> AuthorizeAsync(string directory, ToolExecutionContext context, CancellationToken cancellationToken);
}
