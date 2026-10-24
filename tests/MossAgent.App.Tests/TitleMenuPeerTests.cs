using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TitleMenuPeerTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static void Invoke(Control control) =>
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(control)).Invoke();

    private static AppButton TitleButton(MainWindow window, string content) =>
        Assert.Single(window.GetVisualDescendants().OfType<AppButton>(), button =>
            Equals(button.Content, content) && button.Classes.Contains("menu-trigger"));

    [Fact]
    public Task TitleEditAndHelpPeersOpenFlyoutsWithExpectedItems() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var window = new MainWindow { DataContext = fixture.Main, Width = 960, Height = 700 };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Invoke(TitleButton(window, "编辑"));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            foreach (var name in new[] { "撤销", "重做", "剪切", "复制", "粘贴", "全选" })
                Assert.Contains(window.GetVisualDescendants().OfType<AppText>(),
                    text => text.Text == name && text.IsEffectivelyVisible);
            Invoke(TitleButton(window, "帮助"));
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Contains(window.GetVisualDescendants().OfType<AppText>(),
                text => text.Text == "键盘快捷键" && text.IsEffectivelyVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
