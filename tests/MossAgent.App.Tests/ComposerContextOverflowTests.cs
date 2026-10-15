using Avalonia;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerContextOverflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 360)]
    [InlineData(true, 360)]
    [InlineData(false, 400)]
    [InlineData(true, 400)]
    [InlineData(false, 740)]
    [InlineData(true, 740)]
    public Task ProjectBranchAndAgent_StayWithinContextSurface(bool dark, double width) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await ConfigureLongNamesAsync(fixture);
        var view = new WorkspaceComposerView { DataContext = fixture.Workspace };
        var window = new AppWindow
        {
            Width = width,
            Height = 640,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var surface = Assert.Single(view.GetVisualDescendants().OfType<AppBorder>(),
                border => border.Classes.Contains("composer-context-surface"));
            var project = Assert.Single(surface.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("composer-project-button"));
            var agent = Assert.Single(surface.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("composer-agent-button"));
            var branch = Assert.Single(surface.GetVisualDescendants().OfType<AppIconLabel>(),
                label => label.Classes.Contains("composer-context-label"));
            AssertContained(project, surface);
            AssertContained(branch, surface);
            AssertContained(agent, surface);
            Assert.True(branch.Bounds.Width >= 24, $"Branch has no usable space: {branch.Bounds}");
            var projectEnd = project.TranslatePoint(default, surface)!.Value.X + project.Bounds.Width;
            var branchStart = branch.TranslatePoint(default, surface)!.Value.X;
            var branchEnd = branchStart + branch.Bounds.Width;
            var agentStart = agent.TranslatePoint(default, surface)!.Value.X;
            Assert.True(projectEnd <= branchStart, $"Project {projectEnd} overlaps branch {branchStart}");
            Assert.True(branchEnd <= agentStart, $"Branch {branchEnd} overlaps agent {agentStart}");
            Assert.Equal(fixture.Workspace.Catalog.SelectedProject!.Name, project.Label);
            Assert.Equal(fixture.Workspace.SelectedAgentLabel, agent.Label);
            Assert.True(project.IsEffectivelyEnabled);
            Assert.True(agent.IsEffectivelyEnabled);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static async Task ConfigureLongNamesAsync(WorkspaceModelSelectionFixture fixture)
    {
        var editor = fixture.Workspace.Catalog.ProjectEditor;
        editor.Edit(fixture.Workspace.Catalog.SelectedProject!);
        editor.Name = string.Concat(Enumerable.Repeat("共享组件长项目名称", 30));
        await editor.AddCommand.ExecuteAsync(null);
        Assert.False(editor.HasStatusError, editor.Status);
        var agents = fixture.Main.Agents;
        await agents.RefreshCommand.ExecuteAsync(null);
        agents.NewCommand.Execute(null);
        agents.Name = "用于检查代理名称过长时仍能保留分支信息和操作入口";
        agents.SystemPrompt = "synthetic prompt";
        agents.SelectedModel = Assert.Single(agents.Models, option => option.Model.Id == fixture.SelectedModel.Id);
        await agents.SaveCommand.ExecuteAsync(null);
        fixture.Workspace.SelectAgentCommand.Execute(Assert.Single(fixture.Workspace.Agents));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DockedInspector_UsesTheFinalComposerWidthRatherThanParentMeasurement(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await ConfigureLongNamesAsync(fixture);
        fixture.Main.IsRightPanelOpen = true;
        var window = new MainWindow
        {
            Width = 980,
            Height = 640,
            DataContext = fixture.Main,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var bar = Assert.Single(window.GetVisualDescendants().OfType<AppComposerContextBar>());
            var surface = Assert.Single(bar.GetVisualDescendants().OfType<AppBorder>());
            var actions = surface.GetVisualDescendants().OfType<AppActionButton>().ToArray();
            Assert.Equal(2, actions.Length);
            Assert.All(actions, action => AssertContained(action, surface));
            var branch = Assert.Single(surface.GetVisualDescendants().OfType<AppIconLabel>(),
                label => label.Classes.Contains("composer-context-label"));
            AssertContained(branch, surface);
            Assert.True(branch.Bounds.Width >= 24);
            Assert.InRange(bar.Bounds.Width, 290, 350);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertContained(Avalonia.Controls.Control control, AppBorder surface)
    {
        var position = control.TranslatePoint(default, surface)!.Value;
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0, $"Empty {control.GetType().Name}: {control.Bounds}");
        Assert.InRange(position.X, 0, surface.Bounds.Width);
        Assert.InRange(position.X + control.Bounds.Width, 0, surface.Bounds.Width);
        Assert.InRange(position.Y, 0, surface.Bounds.Height);
        Assert.InRange(position.Y + control.Bounds.Height, 0, surface.Bounds.Height);
    }
}
