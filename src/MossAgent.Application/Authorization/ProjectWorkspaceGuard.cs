using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Authorization;

public sealed class ProjectWorkspaceGuard(ProjectAuthorizationMonitor monitor, ProjectProfile project,
    Guid taskId, string workingDirectory, IReadOnlyList<string> roots, ITaskWorkspaceGuard? environment = null)
    : ITaskWorkspaceLeaseProvider
{
    private readonly ProjectProfile _project = project with
    { AuthorizedDirectories = project.AuthorizedDirectories.ToArray(), RootAuthorizations = project.RootAuthorizations.ToArray() };
    private readonly string[] _roots = roots.ToArray();

    public async Task ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        ValidateContext(context);
        await monitor.ValidateAsync(_project, cancellationToken).ConfigureAwait(false);
        if (environment is not null) await environment.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
        ValidateContext(context);
    }

    public async Task<ITaskWorkspaceLease> AcquireAsync(ToolExecutionContext context, CancellationToken cancellationToken)
    {
        await ValidateAsync(context, cancellationToken).ConfigureAwait(false);
        var lease = await monitor.AcquireAsync(_project, cancellationToken).ConfigureAwait(false);
        try
        {
            if (environment is ITaskWorkspaceLeaseProvider provider)
                lease = new CombinedWorkspaceLease(lease, await provider.AcquireAsync(context, cancellationToken).ConfigureAwait(false));
            await ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            return lease;
        }
        catch { await lease.DisposeAsync().ConfigureAwait(false); throw; }
    }

    private void ValidateContext(ToolExecutionContext context)
    {
        var comparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        if (context.TaskId != taskId || !comparer.Equals(context.WorkingDirectory, workingDirectory)
            || !_roots.SequenceEqual(context.AuthorizedRoots, comparer)) throw new WorkspaceAuthorizationException();
    }
}
