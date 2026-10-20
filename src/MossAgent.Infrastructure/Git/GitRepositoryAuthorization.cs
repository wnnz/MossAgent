using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;

namespace MossAgent.Infrastructure.Git;

public sealed class GitRepositoryAuthorization(GitProcessRunner git, TimeProvider? timeProvider = null) : IGitRepositoryAuthorization
{
    public Task<ToolGitRepository> AuthorizeAsync(string directory, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var project = new ProjectProfile(context.TaskId, "tool-workspace", directory, context.AuthorizedRoots.ToArray(), DateTimeOffset.MinValue);
        return Task.Run(async () =>
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30), timeProvider ?? TimeProvider.System);
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
            try
            {
                var repository = await new GitWorktreeInspector(git).ReadProjectAsync(project, false, operation.Token).ConfigureAwait(false);
                operation.Token.ThrowIfCancellationRequested();
                return new ToolGitRepository(repository.Root, repository.GitDirectory, repository.CommonDirectory);
            }
            catch (OperationCanceledException failure) when (!cancellationToken.IsCancellationRequested && deadline.IsCancellationRequested)
            {
                throw new TimeoutException("Git repository authorization timed out.", failure);
            }
        }, cancellationToken);
    }
}
