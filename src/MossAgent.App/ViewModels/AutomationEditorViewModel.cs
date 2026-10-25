using System.Globalization;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Application.Automations;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationEditorViewModel : ObservableObject
{
    private string _name = string.Empty;
    private string _prompt = string.Empty;
    private string _time = "09:00";
    private ProjectProfile? _project;
    private ModelPickerItemViewModel? _model;
    private AutomationChoice<AutomationFrequency>? _frequency;
    private AutomationChoice<DayOfWeek>? _weekday;
    private AutomationChoice<ApprovalPolicy>? _policy;
    private TimeZoneInfo? _zone;
    private decimal _interval = 1;
    private decimal _timeout = 30;
    private bool _isOpen;
    private bool _isInitializing;
    private readonly TimeProvider _clock;
    private string _schedulePreview = AutomationText.Get("PreviewIncomplete");
    private string _snapshotSummary = AutomationText.Get("SnapshotIncomplete");
    public long ContextVersion { get; private set; }
    public long DraftRevision { get; private set; }

    public AutomationEditorViewModel(TimeProvider? clock = null) => _clock = clock ?? TimeProvider.System;

    public AutomationSchedule? Original { get; private set; }
    public IReadOnlyList<ProjectProfile> Projects { get; private set; } = [];
    public IReadOnlyList<ModelPickerItemViewModel> Models { get; private set; } = [];
    public IReadOnlyList<TimeZoneInfo> TimeZones { get; } = TimeZoneInfo.GetSystemTimeZones();
    public IReadOnlyList<AutomationChoice<AutomationFrequency>> Frequencies { get; } =
        Enum.GetValues<AutomationFrequency>().Select(value => new AutomationChoice<AutomationFrequency>(value, AutomationText.Get(value.ToString()))).ToArray();
    public IReadOnlyList<AutomationChoice<DayOfWeek>> Weekdays { get; } =
        Enum.GetValues<DayOfWeek>().Select(value => new AutomationChoice<DayOfWeek>(value, AutomationText.Get($"Day{(int)value}"))).ToArray();
    public IReadOnlyList<AutomationChoice<ApprovalPolicy>> Policies { get; } =
        Enum.GetValues<ApprovalPolicy>().Select(value => new AutomationChoice<ApprovalPolicy>(value, ApprovalPolicyText.Label(value))).ToArray();

    public bool IsOpen
    {
        get => _isOpen;
        set { if (_isOpen != value) { ContextVersion++; SetProperty(ref _isOpen, value); } }
    }
    public string Name { get => _name; set => SetProperty(ref _name, value ?? string.Empty); }
    public string Prompt { get => _prompt; set => SetProperty(ref _prompt, value ?? string.Empty); }
    public string Time { get => _time; set => SetProperty(ref _time, value ?? string.Empty); }
    public string SchedulePreview { get => _schedulePreview; private set => SetProperty(ref _schedulePreview, value); }
    public string SnapshotSummary { get => _snapshotSummary; private set => SetProperty(ref _snapshotSummary, value); }
    public decimal IntervalHours { get => _interval; set => SetProperty(ref _interval, value); }
    public decimal TimeoutMinutes { get => _timeout; set => SetProperty(ref _timeout, value); }
    public ProjectProfile? SelectedProject { get => _project; set => SetProperty(ref _project, value); }
    public ModelPickerItemViewModel? SelectedModel { get => _model; set => SetProperty(ref _model, value); }
    public TimeZoneInfo? SelectedTimeZone { get => _zone; set => SetProperty(ref _zone, value); }
    public AutomationChoice<DayOfWeek>? SelectedWeekday { get => _weekday; set => SetProperty(ref _weekday, value); }
    public AutomationChoice<ApprovalPolicy>? SelectedPolicy { get => _policy; set => SetProperty(ref _policy, value); }
    public AutomationChoice<AutomationFrequency>? SelectedFrequency
    {
        get => _frequency;
        set
        {
            if (!SetProperty(ref _frequency, value)) return;
            OnPropertyChanged(nameof(IsHourly));
            OnPropertyChanged(nameof(IsWeekly));
        }
    }
    public bool IsHourly => SelectedFrequency?.Value == AutomationFrequency.Hourly;
    public bool IsWeekly => SelectedFrequency?.Value == AutomationFrequency.Weekly;

    public void Open(AutomationSchedule? schedule, IReadOnlyList<ProjectProfile> projects,
        IReadOnlyList<ModelPickerItemViewModel> models)
    {
        var wasInitializing = _isInitializing;
        _isInitializing = true;
        try
        {
            ContextVersion++;
            Original = schedule;
            Projects = projects;
            Models = models;
            OnPropertyChanged(nameof(Projects));
            OnPropertyChanged(nameof(Models));
            Name = schedule?.Name ?? string.Empty;
            Prompt = schedule?.Prompt ?? string.Empty;
            SelectedProject = projects.FirstOrDefault(item => item.Id == schedule?.ProjectId);
            SelectedModel = models.FirstOrDefault(item => item.Provider.Id == schedule?.ProviderId && item.Model.Id == schedule?.ModelId);
            SelectedFrequency = Frequencies.First(item => item.Value == (schedule?.Frequency ?? AutomationFrequency.Daily));
            SelectedWeekday = Weekdays.First(item => item.Value == (schedule?.Weekday ?? DayOfWeek.Monday));
            SelectedPolicy = Policies.First(item => item.Value == (schedule?.ApprovalPolicy ?? ApprovalPolicy.ReadOnly));
            SelectedTimeZone = TimeZones.FirstOrDefault(item => item.Id == (schedule?.TimeZoneId ?? TimeZoneInfo.Local.Id));
            Time = schedule is null ? "09:00" : $"{schedule.Hour:00}:{schedule.Minute:00}";
            IntervalHours = schedule?.IntervalHours ?? 1;
            TimeoutMinutes = schedule?.TimeoutMinutes ?? 30;
        }
        finally
        {
            _isInitializing = wasInitializing;
            if (!_isInitializing) UpdateSchedulePreview();
        }
        IsOpen = true;
    }

    internal bool AcceptSaved(AutomationSchedule saved, long context, long revision, bool close)
    {
        if (!IsOpen || ContextVersion != context) return false;
        Original = saved;
        OnPropertyChanged(nameof(Original));
        UpdateSnapshotSummary();
        if (!close || ContextVersion != context || !IsOpen || DraftRevision != revision) return false;
        IsOpen = false;
        return true;
    }

    internal void RecordSaved(AutomationSchedule saved, long context)
    {
        if (!IsOpen || ContextVersion != context) return;
        Original = saved;
        OnPropertyChanged(nameof(Original));
        UpdateSnapshotSummary();
    }

    public AutomationSchedule Build(DateTimeOffset now)
    {
        var time = TimeOnly.MinValue;
        if (SelectedProject is null || SelectedModel is null || SelectedTimeZone is null
            || SelectedFrequency is null || SelectedPolicy is null || SelectedWeekday is null
            || !IsHourly && !TimeOnly.TryParseExact(Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time)
            || decimal.Truncate(IntervalHours) != IntervalHours || decimal.Truncate(TimeoutMinutes) != TimeoutMinutes)
            throw new ArgumentException("Invalid automation editor state.");
        var schedule = new AutomationSchedule(Original?.Id ?? Guid.NewGuid(), Name.Trim(), Prompt.Trim(),
            SelectedProject.Id, SelectedModel.Provider.Id, SelectedModel.Model.Id, SelectedFrequency.Value,
            checked((int)IntervalHours), time.Hour, time.Minute, SelectedWeekday.Value, SelectedTimeZone.Id,
            SelectedPolicy.Value, checked((int)TimeoutMinutes), Original?.IsEnabled ?? false, now, Original?.Revision ?? 0);
        return schedule with { NextRunAt = AutomationTiming.Next(schedule, now) };
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs args)
    {
        var changed = args.PropertyName is nameof(Name) or nameof(Prompt) or nameof(Time) or nameof(SelectedProject)
            or nameof(SelectedModel) or nameof(SelectedTimeZone) or nameof(SelectedFrequency) or nameof(SelectedWeekday)
            or nameof(SelectedPolicy) or nameof(IntervalHours) or nameof(TimeoutMinutes);
        if (changed) DraftRevision++;
        base.OnPropertyChanged(args);
        if (changed && !_isInitializing) UpdateSchedulePreview();
    }

    private void UpdateSchedulePreview()
    {
        try
        {
            var schedule = Build(_clock.GetUtcNow());
            var local = TimeZoneInfo.ConvertTime(schedule.NextRunAt, SelectedTimeZone!);
            SchedulePreview = AutomationText.Format("SchedulePreview", local, SelectedTimeZone!.DisplayName);
        }
        catch (Exception exception) when (exception is ArgumentException or TimeZoneNotFoundException or InvalidTimeZoneException or OverflowException)
        { SchedulePreview = AutomationText.Get("PreviewIncomplete"); }
        UpdateSnapshotSummary();
    }

    private void UpdateSnapshotSummary()
    {
        var snapshot = Original?.AuthorizationSnapshot;
        if (snapshot is not null)
        {
            SnapshotSummary = AutomationText.Format("SnapshotCurrent",
                $"{snapshot.ModelName} · {snapshot.ProviderName}", ApprovalPolicyText.Label(snapshot.ApprovalPolicy),
                snapshot.AuthorizedDirectories.Count, snapshot.Generation);
        }
        else if (Original is not null)
        {
            SnapshotSummary = AutomationText.Get("SnapshotMissingHint");
        }
        else if (SelectedProject is not null && SelectedModel is not null && SelectedPolicy is not null)
        {
            SnapshotSummary = AutomationText.Format("SnapshotPending",
                $"{SelectedModel.DisplayName} · {SelectedModel.Provider.Name}",
                ApprovalPolicyText.Label(SelectedPolicy.Value), SelectedProject.AuthorizedDirectories.Count);
        }
        else
        {
            SnapshotSummary = AutomationText.Get("SnapshotIncomplete");
        }
    }
}
