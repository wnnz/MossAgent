using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerContextComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("picker")]
    [InlineData("command")]
    public Task MissingProjectDependencies_DisableTheEntry(string missing) => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        if (missing == "picker") bar.ProjectPickerContent = null;
        if (missing == "command") bar.OpenProjectCommand = null;
        var window = new AppWindow { Content = bar, Width = 500, Height = 200 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.False(bar.IsProjectAvailable);
            Assert.False(bar.FindControl<AppActionButton>("ProjectButton")!.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProjectCommandCanExecute_RemainsAuthoritative() => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        bar.OpenProjectCommand = new RelayCommand(() => throw new InvalidOperationException(), () => false);
        var window = new AppWindow { Content = bar, Width = 500, Height = 200 };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(bar.IsProjectAvailable);
            Assert.False(bar.FindControl<AppActionButton>("ProjectButton")!.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingOrRejectedAgentCommand_DisablesMenuActions(bool rejected) => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        bar.SelectAgentCommand = rejected ? new RelayCommand<AgentProfile?>(_ => throw new InvalidOperationException(), _ => false) : null;
        var window = new AppWindow { Content = bar, Width = 500, Height = 300 };
        window.Show();
        var button = bar.FindControl<AppActionButton>("AgentButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        try
        {
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = Assert.IsType<AppCard>(flyout.Content);
            var actions = content.GetVisualDescendants().OfType<AppButton>().ToArray();
            Assert.Equal(2, actions.Length);
            Assert.All(actions, action => Assert.False(action.IsEffectivelyEnabled));
            Assert.Equal(rejected, button.IsEffectivelyEnabled);
        }
        finally { flyout.Hide(); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AgentMenu_ForwardsTheSelectedTargetAndDefaultWithoutWindowViewModelAssumptions() => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        var selections = new List<AgentProfile?>();
        bar.SelectAgentCommand = new RelayCommand<AgentProfile?>(selections.Add);
        var window = new AppWindow { Content = bar, Width = 500, Height = 300 };
        window.Show();
        var button = bar.FindControl<AppActionButton>("AgentButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        try
        {
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var content = Assert.IsType<AppCard>(flyout.Content);
            var actions = content.GetVisualDescendants().OfType<AppButton>().ToArray();
            Assert.Equal(2, actions.Length);
            var profile = Assert.Single(bar.Agents!);
            var choice = Assert.Single(actions, action => ReferenceEquals(action.CommandParameter, profile));
            Assert.Same(bar.SelectAgentCommand, choice.Command);
            Assert.True(choice.IsEffectivelyEnabled);
            choice.Command!.Execute(choice.CommandParameter);
            var reset = Assert.Single(actions, action => action.CommandParameter is null);
            Assert.Equal(bar.DefaultAgentLabel, reset.Content);
            reset.Command!.Execute(null);
            Assert.Equal(new AgentProfile?[] { profile, null }, selections);
        }
        finally { flyout.Hide(); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ProjectPicker_InheritsBusinessContextAndClosesOnTargetChangeOrDetach(bool detach) => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        var businessContext = new object();
        bar.DataContext = businessContext;
        var window = new AppWindow { Content = bar, Width = 500, Height = 300 };
        window.Show();
        var button = bar.FindControl<AppActionButton>("ProjectButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        try
        {
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var host = Assert.IsType<AppContentControl>(flyout.Content);
            Assert.Same(businessContext, host.DataContext);
            Assert.Same(bar.ProjectPickerContent, host.Content);
            Assert.Same(businessContext, bar.ProjectPickerContent!.DataContext);
            Assert.True(flyout.IsOpen);
            if (detach) window.Content = null;
            else bar.Project = bar.Project! with { Id = Guid.NewGuid() };
            Assert.False(flyout.IsOpen);
        }
        finally { flyout.Hide(); window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ResizeAndAgentAvailability_KeepTheSameControlsAndBoundActions() => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        var window = new AppWindow { Content = bar, Width = 740, Height = 300 };
        window.Show();
        try
        {
            var project = bar.FindControl<AppActionButton>("ProjectButton")!;
            var agent = bar.FindControl<AppActionButton>("AgentButton")!;
            foreach (var width in new[] { 360, 740, 400 })
            {
                window.Width = width;
                window.UpdateLayout();
                Assert.Same(project, bar.FindControl<AppActionButton>("ProjectButton"));
                Assert.Same(agent, bar.FindControl<AppActionButton>("AgentButton"));
                Assert.Same(bar.OpenProjectCommand, project.Command);
                var branch = Assert.Single(bar.GetVisualDescendants().OfType<AppIconLabel>(),
                    label => label.Classes.Contains("composer-context-label"));
                Assert.True(branch.Bounds.Width >= 24);
                var icons = bar.GetVisualDescendants().OfType<AppIcon>().Where(icon => icon.IsEffectivelyVisible).ToArray();
                Assert.Equal(3, icons.Length);
                Assert.All(icons, icon => Assert.Equal(12, icon.Size));
                var centers = icons.Select(icon => icon.TranslatePoint(default, bar)!.Value.Y
                    + icon.Bounds.Height / 2).ToArray();
                Assert.InRange(centers.Max() - centers.Min(), 0, 0.5);
            }
            var flyout = Assert.IsType<AppFlyout>(agent.Flyout);
            flyout.ShowAt(agent);
            bar.HasAgents = false;
            window.UpdateLayout();
            Assert.False(flyout.IsOpen);
            Assert.False(agent.IsVisible);
            Assert.False(agent.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppComposerContextBar CreateBar() => new()
    {
        Project = new ProjectProfile(Guid.NewGuid(), "检查共享上下文栏中的长项目名称", Path.GetTempPath(), [], DateTimeOffset.UtcNow),
        BranchLabel = "feature/very-long-branch-name",
        AgentName = "检查共享上下文栏中的长代理名称",
        HasAgents = true,
        Agents = [new AgentProfile(Guid.NewGuid(), "验收代理", "synthetic prompt", Guid.NewGuid(), Guid.NewGuid(), "high", true)],
        ProjectPickerContent = new AppText { Text = "picker content" },
        OpenProjectCommand = new RelayCommand(() => { }),
        SelectAgentCommand = new RelayCommand<AgentProfile?>(_ => { })
    };

    [Fact]
    public Task LateAgentLoad_RecalculatesPreviouslyMeasuredProjectColumns() => _session.Dispatch(() =>
    {
        var bar = CreateBar();
        bar.Project = bar.Project! with { Name = string.Concat(Enumerable.Repeat("共享组件长项目名称", 30)) };
        bar.HasAgents = false;
        var window = new AppWindow { Content = bar, Width = 360, Height = 300 };
        window.Show();
        try
        {
            window.UpdateLayout();
            bar.HasAgents = true;
            window.UpdateLayout();
            var layout = bar.FindControl<Grid>("ContextLayout")!;
            Assert.All(layout.ColumnDefinitions, column => Assert.True(column.Width.IsStar));
            var branch = Assert.Single(bar.GetVisualDescendants().OfType<AppIconLabel>(),
                label => label.Classes.Contains("composer-context-label"));
            Assert.True(branch.Bounds.Width >= 24, $"Stale branch width: {branch.Bounds}");
            var agent = bar.FindControl<AppActionButton>("AgentButton")!;
            var position = agent.TranslatePoint(default, layout)!.Value;
            Assert.InRange(position.X + agent.Bounds.Width, 0, layout.Bounds.Width);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
