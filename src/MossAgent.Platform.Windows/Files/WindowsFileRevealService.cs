using MossAgent.Tools.Abstractions.Files;

namespace MossAgent.Platform.Windows.Files;

public sealed class WindowsFileRevealService : IFileRevealService
{
    private readonly Action<string, Action, CancellationToken> _reveal;
    public WindowsFileRevealService() : this(WindowsShellSelection.Reveal) { }
    internal WindowsFileRevealService(Action<string, Action, CancellationToken> reveal) => _reveal = reveal;

    public Task RevealAsync(string path, Action validatePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(validatePath);
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("An absolute file path is required.");
        cancellationToken.ThrowIfCancellationRequested();
        var canonical = Path.GetFullPath(path);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                validatePath();
                _reveal(canonical, validatePath, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                completion.TrySetResult();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { completion.TrySetCanceled(cancellationToken); }
            catch (Exception exception) { completion.TrySetException(exception); }
        }) { IsBackground = true, Name = "MossAgent file presentation" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }
}
