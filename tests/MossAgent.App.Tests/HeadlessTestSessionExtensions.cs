using Avalonia.Headless;

namespace MossAgent.App.Tests;

internal static class HeadlessTestSessionExtensions
{
    public static Task DispatchAsync(this HeadlessUnitTestSession session, Func<Task> action,
        CancellationToken cancellationToken) => session.Dispatch<bool>(async () =>
    {
        await action();
        return true;
    }, cancellationToken);
}
