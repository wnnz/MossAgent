using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerResponsiveLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ResizingAndProjectChanges_KeepDraftPolicyAndActions(bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var workspace = fixture.Workspace;
        var project = workspace.Catalog.SelectedProject;
        var draft = workspace.ComposerText;
        var view = new WorkspaceComposerView { DataContext = workspace };
        var window = new AppWindow
        {
            Width = 360,
            Height = 640,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            foreach (var width in new[] { 360, 800, 460, 320, 800, 360 })
            {
                window.Width = width;
                Flush(window);
                AssertToolbar(view, width < 440);
                Assert.Equal(draft, workspace.ComposerText);
            }
            workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
            Flush(window);
            AssertToolbar(view, true);
            workspace.Catalog.NewChatCommand.Execute(null);
            Flush(window);
            AssertToolbar(view, false);
            Assert.False(Assert.Single(view.GetVisualDescendants().OfType<AppApprovalPolicyButton>()).IsVisible);
            foreach (var width in new[] { 320, 800, 360 })
            {
                window.Width = width;
                Flush(window);
                AssertToolbar(view, false);
            }
            workspace.Catalog.SelectedProject = project;
            Flush(window);
            AssertToolbar(view, true);
            Assert.Equal(ApprovalPolicy.ReadOnly, workspace.ApprovalPolicy);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertToolbar(WorkspaceComposerView view, bool stacked)
    {
        var toolbar = view.FindControl<Grid>("ComposerToolbar")!;
        var model = view.FindControl<AppActionButton>("ModelPickerButton")!;
        var send = view.FindControl<AppIconButton>("SendButton")!;
        var attach = Assert.Single(view.GetVisualDescendants().OfType<AppIconButton>(), button => button.Classes.Contains("composer-attach"));
        Assert.True(attach.IsEffectivelyVisible);
        Assert.True(attach.IsEffectivelyEnabled);
        Assert.Equal(stacked, toolbar.Classes.Contains("stacked"));
        Assert.Equal(stacked ? 1 : 0, Grid.GetRow(model));
        Assert.Equal(Grid.GetRow(model), Grid.GetRow(send));
        Assert.Equal(stacked ? 3 : 1, Grid.GetColumnSpan(model));
        var origin = model.TranslatePoint(default, toolbar)!.Value;
        Assert.InRange(origin.X, 0, toolbar.Bounds.Width);
        Assert.InRange(origin.X + model.Bounds.Width, 0, send.Bounds.X);
        Assert.Equal(new Size(28, 28), send.Bounds.Size);
    }

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
