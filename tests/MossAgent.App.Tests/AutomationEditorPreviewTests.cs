using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationEditorPreviewTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(AutomationFrequency.Daily, 8, 9)]
    [InlineData(AutomationFrequency.Weekly, 12, 9)]
    [InlineData(AutomationFrequency.Hourly, 8, 14)]
    public Task PreviewMatchesRealTimingAndDoesNotEnableOrExecutePlan(AutomationFrequency frequency, int day, int hour) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
        var schedule = new AutomationSchedule(Guid.NewGuid(), "合成计划", "只读审查", fixture.Workspace.Catalog.SelectedProject!.Id,
            fixture.SelectedProvider.Id, fixture.SelectedModel.Id, frequency, 6, 9, 0, DayOfWeek.Monday,
            TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, false, now);
        var editor = new AutomationEditorViewModel(new PreviewTestTimeProvider(now));
        editor.Open(schedule, [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        var expected = new DateTimeOffset(2026, 10, day, hour, 0, 0, TimeSpan.Zero);
        Assert.Equal(expected, editor.Build(now).NextRunAt);
        Assert.Contains($"2026/10/{day:00} {hour:00}:00", editor.SchedulePreview);
        Assert.False(editor.Build(now).IsEnabled);
        Assert.Empty(fixture.Server.Requests);
        Assert.Empty(await fixture.Automations.GetAllAsync(fixture.Token));
        editor.Time = "invalid";
        if (frequency == AutomationFrequency.Hourly)
        {
            Assert.Equal(expected, editor.Build(now).NextRunAt);
            Assert.DoesNotContain(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => editor.Build(now));
            Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InvalidFieldsClearOldPreview_ChangingFrequencyAndZoneRecomputesIt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
        var editor = new AutomationEditorViewModel(new PreviewTestTimeProvider(now));
        editor.Open(null, [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        editor.Name = "合成计划";
        editor.Prompt = "只读审查";
        editor.SelectedProject = editor.Projects[0];
        editor.SelectedModel = editor.Models[0];
        editor.SelectedTimeZone = TimeZoneInfo.Utc;
        editor.Time = "09:00";
        Assert.Contains("2026/10/08 09:00", editor.SchedulePreview);
        editor.SelectedFrequency = editor.Frequencies.Single(item => item.Value == AutomationFrequency.Hourly);
        editor.IntervalHours = 3;
        Assert.Contains("2026/10/08 11:00", editor.SchedulePreview);
        editor.IntervalHours = 1.5m;
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        editor.IntervalHours = 3;
        editor.TimeoutMinutes = 241;
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        editor.TimeoutMinutes = 30;
        editor.SelectedModel = null;
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task OpenAndReopen_CalculateOnlyTheCompleteDraftAndRetainFieldRevisions() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var now = new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
        var clock = new PreviewTestTimeProvider(now);
        var editor = new AutomationEditorViewModel(clock);
        var schedule = AutomationHistoryTests.Item(fixture).Schedule with { TimeZoneId = TimeZoneInfo.Utc.Id };
        var revisions = 0;
        var previews = new List<string>();
        editor.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(AutomationEditorViewModel.SchedulePreview)) previews.Add(editor.SchedulePreview);
            if (args.PropertyName is nameof(AutomationEditorViewModel.Name) or nameof(AutomationEditorViewModel.Prompt)
                or nameof(AutomationEditorViewModel.Time) or nameof(AutomationEditorViewModel.SelectedProject)
                or nameof(AutomationEditorViewModel.SelectedModel) or nameof(AutomationEditorViewModel.SelectedTimeZone)
                or nameof(AutomationEditorViewModel.SelectedFrequency) or nameof(AutomationEditorViewModel.SelectedWeekday)
                or nameof(AutomationEditorViewModel.SelectedPolicy) or nameof(AutomationEditorViewModel.SelectedScopePolicy)
                or nameof(AutomationEditorViewModel.IntervalHours)
                or nameof(AutomationEditorViewModel.TimeoutMinutes)) revisions++;
        };

        editor.Open(schedule, [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);

        Assert.Equal(1, clock.UtcNowReads);
        Assert.Equal(revisions, editor.DraftRevision);
        Assert.Equal(editor.SchedulePreview, Assert.Single(previews));
        Assert.Contains("2026/10/08 09:00", editor.SchedulePreview);
        previews.Clear();
        var previousContext = editor.ContextVersion;
        var updated = schedule with { Name = "另一计划", Prompt = "另一提示词", Hour = 14, IntervalHours = 2, TimeoutMinutes = 60 };

        editor.Open(updated, editor.Projects, editor.Models);

        Assert.Equal(2, clock.UtcNowReads);
        Assert.Equal(revisions, editor.DraftRevision);
        Assert.True(editor.ContextVersion > previousContext);
        Assert.Same(updated, editor.Original);
        Assert.Equal(editor.SchedulePreview, Assert.Single(previews));
        Assert.Contains("2026/10/08 14:00", editor.SchedulePreview);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("name")]
    [InlineData("prompt")]
    [InlineData("policy")]
    public Task EditsAfterOpen_StillInvalidatePreviewAndAdvanceDraftRevision(string field) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var clock = new PreviewTestTimeProvider(new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero));
        var editor = new AutomationEditorViewModel(clock);
        var schedule = AutomationHistoryTests.Item(fixture).Schedule with { TimeZoneId = TimeZoneInfo.Utc.Id };
        editor.Open(schedule, [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        var reads = clock.UtcNowReads;
        var revision = editor.DraftRevision;
        switch (field)
        {
            case "name": editor.Name = " "; break;
            case "prompt": editor.Prompt = " "; break;
            case "policy": editor.SelectedPolicy = null; break;
        }

        Assert.Equal(reads + 1, clock.UtcNowReads);
        Assert.Equal(revision + 1, editor.DraftRevision);
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        Assert.Throws<ArgumentException>(() => editor.Build(clock.GetUtcNow()));
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedInitialization_DoesNotSuppressLaterPreviewUpdates() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var clock = new PreviewTestTimeProvider(new DateTimeOffset(2026, 10, 8, 8, 0, 0, TimeSpan.Zero));
        var editor = new AutomationEditorViewModel(clock);
        var schedule = AutomationHistoryTests.Item(fixture).Schedule with { TimeZoneId = TimeZoneInfo.Utc.Id };
        editor.Open(schedule, [fixture.Workspace.Catalog.SelectedProject!], [new(fixture.SelectedProvider, fixture.SelectedModel)]);
        Assert.Throws<InvalidOperationException>(() => editor.Open(
            schedule with { Frequency = (AutomationFrequency)999 }, editor.Projects, editor.Models));
        var reads = clock.UtcNowReads;

        editor.Time = "invalid";

        Assert.Equal(reads + 1, clock.UtcNowReads);
        Assert.Equal(AutomationText.Get("PreviewIncomplete"), editor.SchedulePreview);
        editor.Open(schedule, editor.Projects, editor.Models);
        Assert.Equal(reads + 2, clock.UtcNowReads);
        Assert.Contains("2026/10/08 09:00", editor.SchedulePreview);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);
}
