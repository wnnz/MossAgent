using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ApprovalDecisionExecutionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public Task RenderedDecisionReachesRealFileAndAuditPipeline(bool dark, bool approve) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "beta\n");
        var main = fixture.Services.GetRequiredService<MainWindowViewModel>();
        Assert.Same(fixture.Workspace, main.Workspace);
        Assert.Same(fixture.Approval, main.Approval);
        var window = new MainWindow
        {
            DataContext = main,
            Width = dark ? 526 : 1280,
            Height = dark ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            fixture.Start();
            await fixture.WaitForApprovalAsync();
            fixture.Workspace.ComposerText = "保留未发送的下一条草稿。";
            Flush(window);
            var view = Assert.Single(window.GetVisualDescendants().OfType<ToolApprovalView>());
            Assert.True(view.IsEffectivelyVisible);
            var decision = Assert.Single(view.GetVisualDescendants().OfType<AppApprovalButton>(),
                button => ReferenceEquals(button.Command, approve ? fixture.Approval.ApproveCommand : fixture.Approval.DenyCommand));
            Assert.Equal(fixture.Approval.CurrentRequestId, decision.CommandParameter);
            Assert.True(decision.IsEffectivelyEnabled);
            var point = decision.TranslatePoint(new Point(decision.Bounds.Width / 2, decision.Bounds.Height / 2), window)!.Value;
            Assert.True(new Rect(window.ClientSize).Contains(point));
            var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
            Assert.True(ReferenceEquals(decision, hit) || decision.IsVisualAncestorOf(hit),
                $"Expected the rendered decision at {point}, hit {hit.GetType().Name} ({hit.Bounds}).");
            Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
            Assert.True(main.EmbeddedBrowsers.Host.IsPresentationBlocked);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Flush(window);
            await fixture.WaitForCompletionAsync();
            await fixture.AssertPersistedAsync(approve ? null : "approval_denied", approve, AgentTaskStatus.Completed);
            Assert.Equal(approve ? "beta\n" : "alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
            Assert.Equal("保留未发送的下一条草稿。", fixture.Workspace.ComposerText);
            Assert.Equal(3, fixture.Server.Requests.Count);
            Assert.False(main.EmbeddedBrowsers.Host.IsPresentationBlocked);
            Assert.False(view.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Flush(MainWindow window) => HeadlessRenderedFrame.Flush(window);
}
