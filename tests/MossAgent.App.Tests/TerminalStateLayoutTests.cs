using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Terminal;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TerminalStateLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LongerStateTitleIsArrangedWithoutReopeningPane(bool dark) => _session.Dispatch(() =>
    {
        var source = new TerminalStateProbe();
        using var model = new TerminalPaneViewModel(source, new InspectorProbeToolExecutor());
        var context = new ToolExecutionContext(Guid.NewGuid(), "D:\\synthetic", ["D:\\synthetic"], ApprovalPolicy.FullAccess);
        model.AttachTask(context.TaskId, context.WorkingDirectory, context);
        var pane = new AppTerminalPane { DataContext = model };
        var window = new AppWindow { Width = 280, Height = 620, Content = pane, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            source.Emit(new(context.TaskId, Guid.NewGuid(), 1, TerminalSessionState.Starting));
            Dispatcher.UIThread.RunJobs();
            var empty = Assert.Single(pane.GetVisualDescendants().OfType<AppEmptyState>());
            var label = Assert.Single(empty.GetVisualDescendants().OfType<AppText>(), item => item.Text == model.EmptyTitle);
            Assert.True(empty.IsEffectivelyVisible);
            Assert.Equal(model.EmptyTitle, empty.Title);
            Assert.True(label.Bounds.Width >= label.DesiredSize.Width, $"title={label.Text}, arranged={label.Bounds.Width}, desired={label.DesiredSize.Width}");
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
