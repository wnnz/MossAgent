using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

public static class RootIdentitySessionOperation
{
    public static async Task<IRootIdentitySession> OpenAsync(IRootIdentitySessionFactory factory,
        IReadOnlyList<string> roots, CancellationToken token)
    {
        var pending = factory.OpenAsync(roots, token);
        try { return await pending.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _ = DisposeLateAsync(pending);
            throw;
        }
    }

    private static async Task DisposeLateAsync(Task<IRootIdentitySession> pending)
    {
        try { (await pending.ConfigureAwait(false)).Dispose(); }
        catch (Exception) { /* Observe late native failure without exposing paths or abandoning successful handles. */ }
    }
}
