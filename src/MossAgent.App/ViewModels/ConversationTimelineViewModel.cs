using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace MossAgent.App.ViewModels;

public sealed class ConversationTimelineViewModel : IDisposable
{
    private readonly Dictionary<ChatMessageViewModel, (int Count, bool HasContent)> _observed = [];
    private Dictionary<ChatMessageViewModel, ConversationTurnViewModel> _turns = [];
    private ObservableCollection<ChatMessageViewModel>? _source;
    private bool _isRunning;
    private bool _disposed;

    public ObservableCollection<ConversationTurnViewModel> Turns { get; } = [];

    public void Bind(ObservableCollection<ChatMessageViewModel>? source, bool isRunning)
    {
        if (_disposed) return;
        if (!ReferenceEquals(_source, source))
        {
            Detach();
            _source = source;
            if (_source is not null) _source.CollectionChanged += HandleCollectionChanged;
            if (_source is not null)
                foreach (var message in _source) Observe(message);
        }
        _isRunning = isRunning;
        Refresh();
    }

    public void Detach()
    {
        if (_source is not null) _source.CollectionChanged -= HandleCollectionChanged;
        ClearSubscriptions();
        _source = null;
        _turns = [];
        Turns.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Detach();
    }

    private void HandleCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (_disposed || !ReferenceEquals(sender, _source)) return;
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            ClearSubscriptions();
            if (_source is not null)
                foreach (var message in _source) Observe(message);
        }
        else if (args.Action != NotifyCollectionChangedAction.Move)
        {
            foreach (ChatMessageViewModel message in args.OldItems ?? Array.Empty<ChatMessageViewModel>()) Unobserve(message);
            foreach (ChatMessageViewModel message in args.NewItems ?? Array.Empty<ChatMessageViewModel>()) Observe(message);
        }
        Refresh();
    }

    private void HandleMessageChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || sender is not ChatMessageViewModel message || !_observed.TryGetValue(message, out var observed)) return;
        if (args.PropertyName == nameof(ChatMessageViewModel.HasContent))
        {
            if (observed.HasContent == message.HasContent) return;
            _observed[message] = (observed.Count, message.HasContent);
        }
        else if (args.PropertyName != nameof(ChatMessageViewModel.CreatedAt)) return;
        Refresh();
    }

    private void Observe(ChatMessageViewModel message)
    {
        if (_observed.TryGetValue(message, out var observed))
            _observed[message] = (observed.Count + 1, observed.HasContent);
        else
        {
            _observed.Add(message, (1, message.HasContent));
            message.PropertyChanged += HandleMessageChanged;
        }
    }

    private void Unobserve(ChatMessageViewModel message)
    {
        if (!_observed.TryGetValue(message, out var observed)) return;
        if (observed.Count > 1) _observed[message] = (observed.Count - 1, observed.HasContent);
        else
        {
            message.PropertyChanged -= HandleMessageChanged;
            _observed.Remove(message);
        }
    }

    private void ClearSubscriptions()
    {
        foreach (var message in _observed.Keys) message.PropertyChanged -= HandleMessageChanged;
        _observed.Clear();
    }

    private void Refresh()
    {
        List<ConversationTurnViewModel>? rows = null;
        var rowCount = 0;
        var now = DateTimeOffset.UtcNow;
        for (var start = 0; _source is not null && start < _source.Count;)
        {
            var end = start + 1;
            while (end < _source.Count && !_source[end].IsUserMessage) end++;
            var anchor = _source[start];
            var turn = _turns.GetValueOrDefault(anchor) ?? new ConversationTurnViewModel();
            turn.Refresh(_source, start, end, _isRunning && end == _source.Count, now);
            if (rows is null && (rowCount >= Turns.Count || !ReferenceEquals(Turns[rowCount], turn)))
            {
                rows = new(Turns.Count + 1);
                for (var index = 0; index < rowCount; index++) rows.Add(Turns[index]);
            }
            rows?.Add(turn);
            rowCount++;
            start = end;
        }
        if (rows is null && rowCount == Turns.Count) return;
        RebuildTurnMap(rows ?? (IReadOnlyList<ConversationTurnViewModel>)Turns);
        if (rows is not null) Turns.SynchronizeWith(rows);
        else while (Turns.Count > rowCount) Turns.RemoveAt(Turns.Count - 1);
    }

    private void RebuildTurnMap(IReadOnlyList<ConversationTurnViewModel> rows)
    {
        _turns.Clear();
        var rowIndex = 0;
        for (var index = 0; _source is not null && index < _source.Count; index++)
        {
            if (index == 0 || _source[index].IsUserMessage) _turns[_source[index]] = rows[rowIndex++];
        }
    }
}
