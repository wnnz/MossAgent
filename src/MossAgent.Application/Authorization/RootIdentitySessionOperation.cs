using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Application.Authorization;

public static class RootIdentitySessionOperation
{
    public static Task<IRootIdentitySession> OpenAsync(
        IRootIdentitySessionFactory factory,
        IReadOnlyList<string> roots, CancellationToken token) =>
        AwaitAsync(factory.OpenAsync(roots, token), token);

    public static Task<IRootIdentitySession> OpenTransferableAsync(
        ITransferableRootIdentitySessionFactory factory,
        IReadOnlyList<string> roots, CancellationToken token) =>
        AwaitAsync(factory.OpenTransferableAsync(roots, token), token);

    private static async Task<IRootIdentitySession> AwaitAsync(
        Task<IRootIdentitySession> pending, CancellationToken token)
    {
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
