using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MossAgent.App.ViewModels;

public sealed class ConversationTurnViewModel : ObservableObject
{
    private ChatMessageViewModel? _userMessage;
    private ChatMessageViewModel? _response;
    private string _timestamp = string.Empty;
    private string _summary = string.Empty;
    private TimeSpan? _elapsed;
    private bool _isRunning;
    private bool _isExpanded;
    private bool _initialized;
    private bool _manuallyToggled;
    private DateTimeOffset? _timestampSource;
    private DateTime _timestampLocalDate;
    private int _timestampKey = -1;
    private bool _summaryAssigned;
    private bool _summaryHasElapsed;
    private long _summaryElapsedTicks;
    private bool _summaryRunning;

    public ConversationTurnViewModel() => ToggleCommand = new RelayCommand(
        () => { _manuallyToggled = true; IsExpanded = !IsExpanded; }, () => HasActivities);

    public ObservableCollection<ChatMessageViewModel> Activities { get; } = [];
    public IRelayCommand ToggleCommand { get; }
    public ChatMessageViewModel? UserMessage { get => _userMessage; private set => SetProperty(ref _userMessage, value); }
    public ChatMessageViewModel? Response { get => _response; private set => SetProperty(ref _response, value); }
    public string Timestamp { get => _timestamp; private set => SetProperty(ref _timestamp, value); }
    public string Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public TimeSpan? Elapsed { get => _elapsed; private set => SetProperty(ref _elapsed, value); }
    public bool IsRunning { get => _isRunning; private set => SetProperty(ref _isRunning, value); }
    public bool HasTimestamp => Timestamp.Length > 0;
    public bool HasActivities => Activities.Count > 0;
    public bool HasSummary => HasActivities || Elapsed is not null;
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    internal void Refresh(IReadOnlyList<ChatMessageViewModel> messages, int rangeStart, int rangeEnd,
        bool isRunning, DateTime today)
    {
        var previouslyRunning = IsRunning;
        var hadTimestamp = HasTimestamp;
        var hadActivities = HasActivities;
        var hadSummary = HasSummary;
        UserMessage = messages[rangeStart].IsUserMessage ? messages[rangeStart] : null;
        var last = messages[rangeEnd - 1];
        Response = last.IsAssistantMessage && last.Role == "MossAgent" && last.HasContent ? last : null;
        RefreshActivities(messages, rangeStart, rangeEnd);
        IsRunning = isRunning;
        Elapsed = !isRunning && UserMessage?.CreatedAt is { } start && Response?.CreatedAt is { } end && end >= start
            ? end - start : null;
        Timestamp = CachedTimestamp(Response?.CreatedAt ?? UserMessage?.CreatedAt, today);
        Summary = CachedSummary(Elapsed, isRunning);
        if (!_initialized || previouslyRunning && !isRunning && !_manuallyToggled) IsExpanded = isRunning;
        _initialized = true;
        if (hadTimestamp != HasTimestamp) OnPropertyChanged(nameof(HasTimestamp));
        if (hadSummary != HasSummary) OnPropertyChanged(nameof(HasSummary));
        if (hadActivities != HasActivities)
        {
            OnPropertyChanged(nameof(HasActivities));
            ToggleCommand.NotifyCanExecuteChanged();
        }
    }

    /// <summary>The formatted timestamp depends only on the recorded instant and which
    /// day bucket "now" falls in, so unchanged input reuses the cached string instead of
    /// formatting a new one on every refresh.</summary>
    private string CachedTimestamp(DateTimeOffset? recordedAt, DateTime today)
    {
        if (recordedAt is null)
        {
            _timestampSource = null;
            _timestampKey = -1;
            return string.Empty;
        }
        if (recordedAt != _timestampSource)
        {
            _timestampLocalDate = TimeZoneInfo.ConvertTime(recordedAt.Value, TimeZoneInfo.Local).Date;
            _timestampSource = recordedAt;
            _timestampKey = -1;
        }
        var key = ConversationTimelineText.TimestampKey(_timestampLocalDate, today);
        if (key == _timestampKey) return _timestamp;
        _timestampKey = key;
        return ConversationTimelineText.FormatTimestamp(recordedAt.Value, key);
    }

    /// <summary>Same caching as <see cref="CachedTimestamp"/>: the summary is recomputed
    /// only when the elapsed duration or the running state actually changes.</summary>
    private string CachedSummary(TimeSpan? elapsed, bool isRunning)
    {
        var hasElapsed = elapsed is not null;
        var ticks = elapsed?.Ticks ?? 0;
        if (_summaryAssigned && hasElapsed == _summaryHasElapsed
            && ticks == _summaryElapsedTicks && isRunning == _summaryRunning) return _summary;
        (_summaryAssigned, _summaryHasElapsed, _summaryElapsedTicks, _summaryRunning) =
            (true, hasElapsed, ticks, isRunning);
        return elapsed is { } value ? ConversationTimelineText.Elapsed(value)
            : WorkspaceText.Get(isRunning ? "TimelineRunning" : "TimelineActivities");
    }

    private void RefreshActivities(IReadOnlyList<ChatMessageViewModel> messages, int rangeStart, int rangeEnd)
    {
        List<ChatMessageViewModel>? activities = null;
        var activityCount = 0;
        for (var index = rangeStart; index < rangeEnd; index++)
        {
            var message = messages[index];
            if (ReferenceEquals(message, UserMessage) || ReferenceEquals(message, Response)
                || !message.IsTool && !message.HasContent) continue;
            if (activities is null && (activityCount >= Activities.Count || !ReferenceEquals(Activities[activityCount], message)))
            {
                activities = new(Activities.Count + 1);
                for (var previous = 0; previous < activityCount; previous++) activities.Add(Activities[previous]);
            }
            activities?.Add(message);
            activityCount++;
        }
        if (activities is not null) Activities.SynchronizeWith(activities);
        else while (Activities.Count > activityCount) Activities.RemoveAt(Activities.Count - 1);
    }
}
