using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationHistoryVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LoadingFailureAndEmpty_UseMutuallyExclusiveVisibleStates(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var pending = new TaskCompletionSource<IReadOnlyList<AutomationRun>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => pending.Task };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        var view = new AutomationHistoryView { DataContext = history };
        var window = new Window { Content = view, Width = 260, Height = 500, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            var loading = history.SelectAsync(AutomationHistoryTests.Item(fixture));
            window.UpdateLayout();
            Assert.True(Text(view, AutomationText.HistoryLoading).IsEffectivelyVisible);
            Assert.False(Text(view, AutomationText.NoRuns).IsEffectivelyVisible);
            pending.SetException(new IOException("secret"));
            await loading;
            window.UpdateLayout();
            Assert.False(Text(view, AutomationText.HistoryLoading).IsEffectivelyVisible);
            Assert.False(Text(view, AutomationText.NoRuns).IsEffectivelyVisible);
            Assert.True(Text(view, history.LoadError!).IsEffectivelyVisible);
            var retry = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, AutomationText.Retry));
            Assert.True(retry.IsEffectivelyVisible);
            Assert.True(retry.IsEffectivelyEnabled);
            Assert.Same(history.RefreshCommand, retry.Command);
            repository.Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>([]);
            await history.RefreshCommand.ExecuteAsync(null);
            window.UpdateLayout();
            Assert.True(Text(view, AutomationText.NoRuns).IsEffectivelyVisible);
            Assert.False(retry.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(244, false)]
    [InlineData(300, false)]
    [InlineData(244, true)]
    [InlineData(300, true)]
    public Task Rows_UseEqualCenteredIconsAndRealConversationCommands(int width, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var item = AutomationHistoryTests.Item(fixture);
        var start = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        var runs = Enum.GetValues<AutomationRunStatus>().Select(status => new AutomationRun(Guid.NewGuid(), item.Schedule.Id,
            Guid.NewGuid(), start, status, true, status == AutomationRunStatus.Running ? null : start.AddSeconds(42))).ToArray();
        var repository = new AutomationHistoryProbeRepository { Read = (_, _) => Task.FromResult<IReadOnlyList<AutomationRun>>(runs) };
        using var history = new AutomationHistoryViewModel(repository, fixture.Tasks, fixture.Workspace.Catalog);
        await history.SelectAsync(item);
        var view = new AutomationHistoryView { DataContext = history };
        var window = new Window { Content = view, Width = width, Height = 1200, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var rows = view.GetVisualDescendants().OfType<AppBorder>().Where(control => control.Classes.Contains("automation-run-row")).ToArray();
            var scroll = Assert.Single(view.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.Equal(new Thickness(0, 0, 12, 0), scroll.Padding);
            Assert.Contains("thin-scrollbar", scroll.Classes);
            Assert.Equal(runs.Length, rows.Length);
            foreach (var row in rows)
            {
                var model = Assert.IsType<AutomationRunViewModel>(row.DataContext);
                var status = Assert.Single(row.GetVisualDescendants().OfType<AppIconLabel>());
                var icon = Assert.Single(status.GetVisualDescendants().OfType<AppIcon>());
                var label = Assert.Single(status.GetVisualDescendants().OfType<AppText>());
                Assert.Equal(14, icon.Size);
                Assert.True(icon.IsEffectivelyVisible);
                var iconCenter = icon.TranslatePoint(new Point(icon.Bounds.Width / 2, icon.Bounds.Height / 2), row)!.Value;
                var textCenter = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), row)!.Value;
                Assert.InRange(Math.Abs(iconCenter.Y - textCenter.Y), 0, 0.5);
                var trigger = Text(row, model.TriggerLabel);
                var triggerCenter = trigger.TranslatePoint(new Point(trigger.Bounds.Width / 2, trigger.Bounds.Height / 2), row)!.Value;
                Assert.InRange(Math.Abs(iconCenter.Y - triggerCenter.Y), 0, 0.5);
                Assert.True(triggerCenter.X + trigger.Bounds.Width / 2 <= row.Bounds.Width);
                Assert.Equal(model.IsCompleted, status.Classes.Contains("completed"));
                Assert.Equal(model.IsFailure, status.Classes.Contains("failure"));
                Assert.Equal(model.NeedsAttention, status.Classes.Contains("attention"));
                Assert.Equal(model.HasDetail, Text(row, model.Detail).IsVisible);
                var button = Assert.Single(row.GetVisualDescendants().OfType<AppButton>());
                Assert.Same(history.OpenCommand, button.Command);
                Assert.Same(model, button.CommandParameter);
                Assert.True(button.IsEffectivelyEnabled);
                Assert.True(row.Bounds.Width <= width - 12);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppText Text(Control parent, string text) => Assert.Single(parent.GetVisualDescendants().OfType<AppText>(), control => control.Text == text);
}
