using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceTaskEnvironmentControlTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526)]
    [InlineData(false, 1000)]
    [InlineData(true, 526)]
    [InlineData(true, 1000)]
    public Task Composer_ProvidesBoundKeyboardAccessibleOptionsAndClosesOnLock(bool dark, int width) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var repository = new TestWorkspaceRepository
            {
                SaveTaskHandler = async (_, token) =>
                {
                    entered.TrySetResult();
                    await release.Task.WaitAsync(token);
                }
            };
            await using var workspace = fixture.CreateWorkspace(repository, new WorkspaceRunTracker());
            await workspace.LoadAsync();
            workspace.ComposerText = "验证准备期间锁定工作树选项。";
            var view = new WorkspaceComposerView { DataContext = workspace };
            var window = new AppWindow { Width = width, Height = 300, Content = view,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                window.UpdateLayout();
                var environment = Assert.Single(view.GetVisualDescendants().OfType<WorkspaceTaskEnvironmentView>());
                Assert.Same(workspace.Catalog.Environment, environment.DataContext);
                var button = environment.FindControl<AppActionButton>("EnvironmentButton")!;
                Assert.True(button.IsEffectivelyVisible);
                Assert.True(button.IsEffectivelyEnabled);
                Assert.InRange(button.Bounds.Width, 24, 120);
                var bar = Assert.Single(view.GetVisualDescendants().OfType<AppComposerContextBar>());
                var position = Assert.IsType<Point>(button.TranslatePoint(default, bar));
                Assert.True(position.X >= 0 && position.X + button.Bounds.Width <= bar.Bounds.Width);
                Assert.True(button.Focus());
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
                Dispatcher.UIThread.RunJobs();
                var flyout = Assert.IsType<AppFlyout>(button.Flyout);
                Assert.True(flyout.IsOpen);
                var content = Assert.IsType<AppCard>(flyout.Content);
                var actions = content.GetVisualDescendants().OfType<AppActionButton>().ToArray();
                Assert.Equal(2, actions.Length);
                var worktree = Assert.Single(actions, action => ReferenceEquals(action.Command, workspace.Catalog.Environment.UseWorktreeCommand));
                Assert.True(worktree.IsEffectivelyEnabled);
                SaveEvidence(window, dark, width);
                worktree.Command!.Execute(null);
                Dispatcher.UIThread.RunJobs();
                Assert.True(workspace.Catalog.Environment.UseWorktree);
                Assert.Equal(workspace.Catalog.Environment.WorktreeLabel, button.Label);
                Assert.False(flyout.IsOpen);
                workspace.Catalog.Environment.UseProjectDirectoryCommand.Execute(null);
                flyout.ShowAt(button);
                Assert.True(flyout.IsOpen);
                workspace.PrimaryActionCommand.Execute(null);
                await entered.Task.WaitAsync(fixture.Token);
                Dispatcher.UIThread.RunJobs();
                Assert.True(workspace.Catalog.IsLocked);
                Assert.False(flyout.IsOpen);
                Assert.False(button.IsEffectivelyEnabled);
            }
            finally
            {
                release.TrySetResult();
                window.Close();
                await workspace.ShutdownAsync();
            }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Projectless_ComposerHidesEnvironmentWithoutGrantingAccess() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: false);
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = new AppWindow { Width = 526, Height = 300, Content = view };
        window.Show();
        try
        {
            window.UpdateLayout();
            var environment = Assert.Single(view.GetVisualDescendants().OfType<WorkspaceTaskEnvironmentView>());
            Assert.Same(fixture.Workspace.Catalog.Environment, environment.DataContext);
            Assert.False(environment.IsEffectivelyVisible);
            Assert.False(fixture.Workspace.Catalog.Environment.UseWorktree);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void SaveEvidence(AppWindow window, bool dark, int width)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_WORKTREE_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"worktree-dark-{dark}-width-{width}.png"), PngBitmapEncoderOptions.Default);
    }

    [Fact]
    public Task NarrowContext_WithLongProjectAndAgentNamesKeepsAllButtonsSeparate() => _session.Dispatch(() =>
    {
        var project = new ProjectProfile(Guid.NewGuid(), new string('项', 60), "fixture-root", ["fixture-root"], DateTimeOffset.UtcNow);
        var environment = new WorkspaceTaskEnvironmentViewModel();
        environment.SetContext(project, null);
        var picker = new WorkspaceTaskEnvironmentView { DataContext = environment };
        var bar = new AppComposerContextBar
        {
            Project = project, BranchLabel = new string('分', 80), AgentName = new string('代', 60), HasAgents = true,
            ProjectPickerContent = new AppText(), OpenProjectCommand = new RelayCommand(() => { }),
            SelectAgentCommand = new RelayCommand(() => { }), EnvironmentPickerContent = picker
        };
        var window = new AppWindow { Width = 526, Height = 120, Content = bar };
        window.Show();
        try
        {
            foreach (var width in new[] { 526, 360, 740, 360 })
            {
                window.Width = width;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Equal(width, bar.Bounds.Width);
                var projectButton = bar.FindControl<AppActionButton>("ProjectButton")!;
                var environmentButton = picker.FindControl<AppActionButton>("EnvironmentButton")!;
                var agentButton = bar.FindControl<AppActionButton>("AgentButton")!;
                var projectStart = projectButton.TranslatePoint(default, bar)!.Value.X;
                var environmentStart = environmentButton.TranslatePoint(default, bar)!.Value.X;
                var agentStart = agentButton.TranslatePoint(default, bar)!.Value.X;
                var layout = $"width={width}; project={projectStart}/{projectButton.Bounds.Width}; environment={environmentStart}/{environmentButton.Bounds.Width}; agent={agentStart}/{agentButton.Bounds.Width}; bar={bar.Bounds.Width}";
                Assert.True(projectStart >= 0, layout);
                var projectBounds = new Rect(projectButton.TranslatePoint(default, bar)!.Value, projectButton.Bounds.Size);
                var environmentBounds = new Rect(environmentButton.TranslatePoint(default, bar)!.Value, environmentButton.Bounds.Size);
                var agentBounds = new Rect(agentButton.TranslatePoint(default, bar)!.Value, agentButton.Bounds.Size);
                Assert.False(projectBounds.Intersects(environmentBounds), layout);
                Assert.False(environmentBounds.Intersects(agentBounds), layout);
                Assert.False(projectBounds.Intersects(agentBounds), layout);
                Assert.True(agentStart + agentButton.Bounds.Width <= bar.Bounds.Width, layout);
                Assert.True(environmentButton.Bounds.Width >= 24);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
