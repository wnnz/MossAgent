using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppAutomationPeerInvokeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static IInvokeProvider Peer(Control control) =>
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(control));

    [Fact]
    public Task AppButtonPeerInvokeExecutesAndDisabledPeerDoesNot() => _session.Dispatch(() =>
    {
        var calls = 0;
        var enabled = new AppButton { Content = "enabled", Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => calls++) };
        var disabled = new AppButton { Content = "disabled", IsEnabled = false, Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => calls++) };
        var icon = new AppIconButton { Label = "icon", Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => calls++) };
        var disabledIcon = new AppIconButton { Label = "icon-off", IsEnabled = false, Command = new CommunityToolkit.Mvvm.Input.RelayCommand(() => calls++) };
        var window = new AppWindow { Width = 300, Height = 200, Content = new StackPanel { Children = { enabled, disabled, icon, disabledIcon } } };
        window.Show();
        try
        {
            Peer(enabled).Invoke();
            Peer(icon).Invoke();
            Assert.ThrowsAny<Avalonia.Automation.ElementNotEnabledException>(() => Peer(disabled).Invoke());
            Assert.ThrowsAny<Avalonia.Automation.ElementNotEnabledException>(() => Peer(disabledIcon).Invoke());
            Assert.Equal(2, calls);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
