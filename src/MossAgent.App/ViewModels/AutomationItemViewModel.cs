using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class AutomationItemViewModel(AutomationSchedule schedule, string projectLabel, string modelLabel, bool running)
{
    public AutomationSchedule Schedule { get; } = schedule;
    public string Name => Schedule.Name;
    public string Prompt => Schedule.Prompt;
    public string ProjectLabel { get; } = projectLabel;
    public string ModelLabel { get; } = modelLabel;
    public bool IsRunning { get; } = running;
    public string StateLabel => AutomationText.Get(IsRunning ? "Running" : Schedule.IsEnabled ? "Scheduled" : "Paused");
    public string ToggleLabel => AutomationText.Get(Schedule.IsEnabled ? "Pause" : "Enable");
    public string NextLabel => Schedule.IsEnabled
        ? AutomationText.Format("Next", TimeZoneInfo.ConvertTime(Schedule.NextRunAt,
            TimeZoneInfo.FindSystemTimeZoneById(Schedule.TimeZoneId)), Schedule.TimeZoneId)
        : AutomationText.Get("Paused");
    public string TimingLabel => Schedule.Frequency switch
    {
        AutomationFrequency.Hourly => AutomationText.Format("HourlySummary", Schedule.IntervalHours),
        AutomationFrequency.Daily => AutomationText.Format("DailySummary", Schedule.Hour, Schedule.Minute),
        _ => AutomationText.Format("WeeklySummary", AutomationText.Get($"Day{(int)Schedule.Weekday}"), Schedule.Hour, Schedule.Minute)
    };
}
