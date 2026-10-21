namespace MossAgent.App.Tests;

internal static class RootObjectTestDirectory
{
    public static async Task MoveWhenReadersFinishAsync(string source, string destination, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        // Project selection starts a real Git status process. Wait for its transient working-directory handle.
        while (true)
        {
            deadline.Token.ThrowIfCancellationRequested();
            try { Directory.Move(source, destination); return; }
            catch (IOException exception) when ((exception.HResult & 0xffff) == 32)
            { await Task.Delay(15, deadline.Token); }
        }
    }
}
