using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

internal static class ProjectRootObjectValidation
{
    public static async Task<IRootIdentitySession> OpenAsync(IRootIdentitySessionFactory factory, ProjectProfile project,
        CancellationToken token)
    {
        if (project.RootAuthorizations.Count != project.AuthorizedDirectories.Count || project.RootAuthorizations.Count == 0
            || project.RootAuthorizations.Any(item => item?.Identity is null || item.GrantId == Guid.Empty)
            || project.RootAuthorizations.Select(item => item.GrantId).Distinct().Count() != project.RootAuthorizations.Count)
            throw new WorkspaceAuthorizationException();
        IRootIdentitySession session;
        try { session = await RootIdentitySessionOperation.OpenAsync(factory, project.AuthorizedDirectories, token).ConfigureAwait(false); }
        catch (OperationCanceledException) { throw; }
        catch { throw new WorkspaceAuthorizationException(); }
        try
        {
            if (session.Roots.Count != project.RootAuthorizations.Count) throw new WorkspaceAuthorizationException();
            for (var index = 0; index < session.Roots.Count; index++)
                if (!ProjectRootEnrollment.SameObject(project.RootAuthorizations[index].Identity, session.Roots[index]))
                    throw new WorkspaceAuthorizationException();
            token.ThrowIfCancellationRequested();
            return session;
        }
        catch { session.Dispose(); throw; }
    }
}
