using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class CallbackPathDeleteSession(IPathDeleteSession inner,
    Func<IPathDeleteSession, CancellationToken, Task>? validate) : IPathDeleteSession
{
    public Stream? FileContent => inner.FileContent;
    public int PlannedCount => inner.PlannedCount;
    public int DeletedCount => inner.DeletedCount;
    public Task DeleteAsync(Func<CancellationToken, Task> validateAuthorization, CancellationToken cancellationToken) =>
        inner.DeleteAsync(async token =>
        {
            if (validate is not null) await validate(inner, token);
            await validateAuthorization(token);
        }, cancellationToken);
    public void Dispose() => inner.Dispose();
}
