using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ApprovalNavigationDrawerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task DrawerTemporarilyHidesApprovalWithoutLosingQueueOrAcceptingStaleDecision(bool dark, bool settings) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            using var firstCancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
            using var secondCancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
            var window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = 526,
                Height = 601,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                if (settings) fixture.Main.SelectSettingsTabCommand.Execute("5");
                SubagentApprovalInteraction.Flush(window);
                var approval = fixture.Main.Approval;
                var first = Request(approval, "drawer-first", firstCancellation.Token);
                var firstId = approval.CurrentRequestId;
                var second = Request(approval, "drawer-second", secondCancellation.Token);
                SubagentApprovalInteraction.Flush(window);
                var view = SubagentApprovalInteraction.View(window);
                Assert.True(view.IsEffectivelyVisible);
                var toggle = Assert.Single(window.GetVisualDescendants().OfType<AppIconButton>(), button => button.Label == "切换侧边栏");
                SubagentApprovalInteraction.Activate(window, toggle, keyboard: true);
                var pane = settings ? window.FindControl<SettingsOverlay>("SettingsView")!.Navigation
                    : window.FindControl<AppNavigationPanes>("WorkspaceNavigation")!;
                Assert.True(pane.IsOverlayOpen);
                Assert.False(view.IsEffectivelyVisible);
                Assert.Equal(2, approval.PendingCount);
                Assert.False(first.IsCompleted);
                Assert.False(second.IsCompleted);
                Assert.True(fixture.Main.EmbeddedBrowsers.Host.IsPresentationBlocked);

                await firstCancellation.CancelAsync();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
                SubagentApprovalInteraction.Flush(window);
                Assert.False(view.IsEffectivelyVisible);
                Assert.Equal(1, approval.PendingCount);
                Assert.NotEqual(firstId, approval.CurrentRequestId);
                approval.ApproveCommand.Execute(firstId);
                Assert.False(second.IsCompleted);
                SubagentApprovalInteraction.Activate(window, toggle, keyboard: true);
                Assert.False(pane.IsOverlayOpen);
                Assert.True(view.IsEffectivelyVisible);
                SubagentApprovalInteraction.Decide(window, approval, approve: false, keyboard: true);
                Assert.False(await second.WaitAsync(fixture.Token));
                Assert.False(approval.IsPending);
                Assert.Empty(fixture.Server.Requests);
            }
            finally
            {
                await firstCancellation.CancelAsync();
                await secondCancellation.CancelAsync();
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static Task<bool> Request(MossAgent.App.ViewModels.ApprovalViewModel approval, string callId, CancellationToken token)
    {
        var descriptor = new ToolDescriptor("filesystem.replace_text", "合成抽屉审批请求", "{}",
            ToolRiskLevel.Mutation, ToolCapability.FileWrite);
        var request = new ToolRequest(callId, descriptor.Name, JsonSerializer.SerializeToElement(new
        {
            path = @"C:\fixture\draft.txt",
            oldText = "before",
            newText = "after"
        }));
        return approval.RequestAsync(descriptor, request, token).AsTask();
    }
}
