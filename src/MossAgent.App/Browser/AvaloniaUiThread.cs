using Avalonia.Threading;

namespace MossAgent.App.Browser;

internal static class AvaloniaUiThread
{
    public static async Task RunAsync(
        Action action,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            action();
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(
            action, DispatcherPriority.Normal, cancellationToken);
    }

    public static async Task RunAsync(
        Func<Task> action,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            await action();
            return;
        }

        var work = await Dispatcher.UIThread.InvokeAsync(
            action, DispatcherPriority.Normal, cancellationToken);
        await work.WaitAsync(cancellationToken);
    }

    public static async Task<T> RunAsync<T>(
        Func<T> action,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return action();
        }

        return await Dispatcher.UIThread.InvokeAsync(
            action, DispatcherPriority.Normal, cancellationToken);
    }

    public static async Task<T> RunAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        if (Dispatcher.UIThread.CheckAccess())
        {
            return await action();
        }

        var work = await Dispatcher.UIThread.InvokeAsync(
            action, DispatcherPriority.Normal, cancellationToken);
        return await work.WaitAsync(cancellationToken);
    }
}
