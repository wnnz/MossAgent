using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorOverviewGeometryTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, false)]
    [InlineData(1280, false)]
    [InlineData(1920, false)]
    [InlineData(980, true)]
    [InlineData(1280, true)]
    [InlineData(1920, true)]
    public Task Overview_UsesSharedInsetsAndBalancedMetadataGroups(double width, bool dark) => WithWindow((window, model) =>
    {
        window.Width = width;
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        window.UpdateLayout();
        var card = Card(window);
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal(300, card.Bounds.Width);
        Assert.Equal(new Thickness(20, 10, 10, 13), card.Padding);
        var layout = Assert.IsType<StackPanel>(card.Child);
        Assert.Equal(10, layout.Spacing);
        var heading = Assert.IsType<StackPanel>(layout.Children[0]);
        Assert.Contains("inspector-overview-heading", heading.Classes);
        Assert.Equal(4, heading.Spacing);
        var sources = Assert.IsType<Grid>(layout.Children[1]);
        Assert.Contains("inspector-overview-sources", sources.Classes);
        Assert.Equal(new Thickness(0, 6, 0, 0), sources.Margin);
        var sourceList = Assert.IsType<StackPanel>(layout.Children[2]);
        Assert.Contains("inspector-overview-source-list", sourceList.Classes);
        Assert.Equal(5, sourceList.Spacing);
        var title = Assert.Single(heading.GetVisualDescendants().OfType<AppText>(),
            control => control.Text == "Overview fixture");
        var branch = Assert.Single(heading.GetVisualDescendants().OfType<AppText>(),
            control => control.Text == model.Workspace.RepositoryStatus.BranchLabel);
        var titleBounds = Bounds(title, card);
        var branchBounds = Bounds(branch, card);
        Assert.True(titleBounds.Bottom < branchBounds.Top);
        foreach (var control in card.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
        {
            var bounds = Bounds(control, card);
            Assert.True(bounds.Left >= 0 && bounds.Right <= card.Bounds.Width + 0.01);
            Assert.True(bounds.Top >= 0 && bounds.Bottom <= card.Bounds.Height + 0.01);
        }
        Assert.True(Bounds(card, window).Bottom < window.ClientSize.Height);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OverviewCommands_KeepTheExistingInspectorAndDraftBehavior(bool dark) => WithWindow((window, model) =>
    {
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var composer = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceComposerView>());
        var input = Assert.Single(composer.GetVisualDescendants().OfType<AppInput>());
        input.Text = "unsent overview draft";
        var card = Card(window);
        var sources = Assert.Single(card.GetVisualDescendants().OfType<AppActionButton>(),
            control => control.Label == "查看全部");
        Assert.Same(model.SelectInspectorTabCommand, sources.Command);
        Assert.Equal("sources", sources.CommandParameter);
        Assert.True(sources.Command!.CanExecute(sources.CommandParameter));
        sources.Command.Execute(sources.CommandParameter);
        window.UpdateLayout();
        Assert.Equal("sources", model.ActiveInspectorTab);
        Assert.True(model.IsRightPanelOpen);
        Assert.False(card.IsEffectivelyVisible);
        Assert.Equal("unsent overview draft", input.Text);
        model.ToggleRightPanelCommand.Execute(null);
        window.UpdateLayout();
        Assert.True(card.IsEffectivelyVisible);
        Assert.Equal("unsent overview draft", input.Text);
        model.Workspace.Catalog.StartNewTask();
        window.UpdateLayout();
        Assert.False(card.IsEffectivelyVisible);
    });

    private Task WithWindow(Action<MainWindow, MainWindowViewModel> action) => _session.DispatchAsync(async () =>
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-overview-geometry-").FullName;
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellation = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellation);
            var repository = services.GetRequiredService<IWorkspaceRepository>();
            var now = DateTimeOffset.UtcNow;
            var project = new ProjectProfile(Guid.NewGuid(), "Overview fixture", root, [root], now);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Overview conversation", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, now, now, null);
            await repository.SaveProjectAsync(project, cancellation);
            await repository.SaveTaskAsync(task, cancellation);
            var model = services.GetRequiredService<MainWindowViewModel>();
            await model.Workspace.Catalog.ReloadAsync();
            model.Workspace.Catalog.UpsertTask(task);
            var window = new MainWindow { DataContext = model, WindowState = WindowState.Normal, Height = 640 };
            window.Show();
            try { window.UpdateLayout(); action(window, model); }
            finally { window.Close(); }
        }
        finally { SqliteTestPool.Clear(root); Directory.Delete(root, recursive: true); }
    }, TestContext.Current.CancellationToken);

    private static AppCard Card(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<AppCard>(),
        control => control.Classes.Contains("inspector-surface"));

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);
}
