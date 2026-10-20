using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Browser;
using MossAgent.App.Resources;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class PullRequestChatLaunchViewModel : ObservableObject, IAsyncDisposable
{
    private readonly SourceReviewChatCreator _creator;
    private readonly Func<SourceReviewChatBinding?> _capture;
    private readonly Func<IReadOnlyList<PullRequestCheck>> _checks;
    private readonly Func<bool> _available;
    private CancellationTokenSource? _operation;
    private string _status = string.Empty;
    private int _generation;
    private bool _disposed;

    public PullRequestChatLaunchViewModel(SourceReviewChatCreator creator, Func<SourceReviewChatBinding?> capture,
        Func<IReadOnlyList<PullRequestCheck>> checks, Func<bool> available)
    {
        _creator = creator; _capture = capture; _checks = checks; _available = available;
        OpenCommand = new(token => OpenAsync(false, false, token), CanOpen);
        ReadOnlyCommand = new(token => OpenAsync(false, true, token), CanOpen);
        InvestigateCommand = new(token => OpenAsync(true, false, token), () => CanOpen() && _checks().Any(SourceReviewContextBuilder.IsFailure));
    }

    public event Action<AgentTask>? ConversationRequested;
    public Func<AgentTask, string, CancellationToken, Task<bool>>? PrepareDraft { get; set; }
    public GuardedAsyncRelayCommand OpenCommand { get; }
    public GuardedAsyncRelayCommand ReadOnlyCommand { get; }
    public GuardedAsyncRelayCommand InvestigateCommand { get; }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    private bool CanOpen() => !_disposed && _operation is null && _available();
    public void NotifyAvailability() { OpenCommand.NotifyCanExecuteChanged(); ReadOnlyCommand.NotifyCanExecuteChanged(); InvestigateCommand.NotifyCanExecuteChanged(); }
    public void Invalidate() { _generation++; _operation?.Cancel(); Status = string.Empty; NotifyAvailability(); }

    private async Task OpenAsync(bool investigate, bool sourceReads, CancellationToken caller)
    {
        if (!CanOpen() || _capture() is not { } binding) return;
        if (sourceReads) binding = binding with { Mode = SourceReviewChatMode.ReadOnlySource };
        var generation = _generation;
        var evidence = investigate ? SourceReviewContextBuilder.Investigation(_checks()) : null;
        if (investigate && evidence is null) return;
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(caller);
        operation.CancelAfter(TimeSpan.FromSeconds(30)); _operation = operation;
        Status = PullRequestText.Get("ChatOpening"); NotifyAvailability();
        try
        {
            var title = $"PR #{binding.Revision.Number} · {binding.Scope.Owner}/{binding.Scope.Repository} · {binding.Revision.HeadRevision[..8]}";
            if (sourceReads) title += " · " + PullRequestText.Get("ReadOnlyMode");
            var task = await Task.Run(() => _creator.OpenAsync(binding, title, operation.Token), operation.Token).ConfigureAwait(false);
            var draft = evidence is null ? null : PullRequestText.Get("InvestigationPrompt") + "\n" + evidence;
            var prepared = draft is null || (PrepareDraft is { } prepare
                ? await prepare(task, draft, operation.Token).ConfigureAwait(false)
                : await _creator.PrepareDraftAsync(task, binding.Scope, draft, operation.Token).ConfigureAwait(false));
            await AvaloniaUiThread.RunAsync(() =>
            {
                if (_disposed || operation.IsCancellationRequested || generation != _generation) return;
                Status = PullRequestText.Get(prepared ? "ChatOpened" : "ChatDraftPreserved");
                ConversationRequested?.Invoke(task);
            }, CancellationToken.None).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { await ReportAsync(generation, PullRequestText.Get("Cancelled")).ConfigureAwait(false); }
        catch (SourceReviewException exception) { await ReportAsync(generation, PullRequestText.Failure(exception.ErrorCode)).ConfigureAwait(false); }
        catch (Exception) { await ReportAsync(generation, PullRequestText.Get("Failed")).ConfigureAwait(false); }
        finally
        {
            await AvaloniaUiThread.RunAsync(() => { _operation = null; NotifyAvailability(); }, CancellationToken.None).ConfigureAwait(false);
        }
    }

    private Task ReportAsync(int generation, string status) => AvaloniaUiThread.RunAsync(() =>
    { if (!_disposed && generation == _generation) Status = status; }, CancellationToken.None);

    public async ValueTask DisposeAsync()
    {
        _disposed = true; Invalidate(); OpenCommand.Dispose(); ReadOnlyCommand.Dispose(); InvestigateCommand.Dispose();
        foreach (var command in new[] { OpenCommand, ReadOnlyCommand, InvestigateCommand }) if (command.ExecutionTask is { } work) await work.ConfigureAwait(false);
        ConversationRequested = null; PrepareDraft = null;
    }
}
