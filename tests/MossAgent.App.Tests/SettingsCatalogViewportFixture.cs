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

internal sealed class SettingsCatalogViewportFixture : IDisposable
{
    private readonly IDisposable _owner;

    public SettingsCatalogViewportFixture(string page, bool dark, int width, int count = 30)
    {
        if (page == "proxy")
        {
            ProxyRepository = new ProxyMutationProbeRepository();
            for (var index = 0; index < count; index++)
                ProxyRepository.Proxies.Add(new ProxyProfile(Guid.NewGuid(), $"Proxy {index:00}", ProxyProtocol.Http,
                    "127.0.0.1", 8080 + index, null, null, index == 0, true));
            Proxies = new ProxySettingsViewModel(ProxyRepository, Tester);
            _owner = Proxies;
            View = new ProxySettingsPane { DataContext = Proxies };
            Refresh = Proxies.RefreshCommand;
            IsFormOpen = () => Proxies.IsFormOpen;
        }
        else
        {
            Mcp = new McpSettingsReloadFixture();
            Mcp.Repository.Items = Enumerable.Range(0, count).Select(index => McpSettingsReloadFixture.Server($"Server {index:00}")).ToArray();
            _owner = Mcp;
            View = new McpSettingsPane { DataContext = Mcp.Model };
            Refresh = Mcp.Model.ReloadCommand;
            IsFormOpen = () => Mcp.Model.IsFormOpen;
        }
        Window = new AppWindow
        {
            Content = View,
            Width = width,
            Height = 601,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Flush();
    }

    public AppWindow Window { get; }
    public AppUserControl View { get; }
    public IAsyncRelayCommand Refresh { get; }
    public Func<bool> IsFormOpen { get; }
    public ProxyMutationProbeRepository? ProxyRepository { get; }
    public ProxySettingsViewModel? Proxies { get; }
    public McpSettingsReloadFixture? Mcp { get; }
    public RecordingProxyConnectionTester Tester { get; } = new();
    public AppListBox List => Assert.Single(View.GetVisualDescendants().OfType<AppListBox>());
    public AppCard Card => Assert.Single(View.GetVisualDescendants().OfType<AppCard>());
    public AppSettingsPageHeader Header => Assert.Single(View.GetVisualDescendants().OfType<AppSettingsPageHeader>());

    public void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public void Send(Key key)
    {
        Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        Flush();
    }

    public void Press(Control control)
    {
        control.BringIntoView();
        Flush();
        Assert.True(control.IsEffectivelyEnabled);
        Assert.True(control.Focus(NavigationMethod.Tab));
        Send(Key.Space);
    }

    public AppSettingsItemRow SelectLastRow()
    {
        List.ScrollIntoView(List.Items[0]!);
        Flush();
        var first = Assert.IsAssignableFrom<Control>(List.ContainerFromIndex(0));
        Assert.True(first.Focus(NavigationMethod.Tab));
        Send(Key.End);
        Assert.Same(List.Items[List.ItemCount - 1], List.SelectedItem);
        var scroll = Assert.Single(List.GetVisualDescendants().OfType<ScrollViewer>());
        Assert.True(scroll.Offset.Y > 0);
        return Assert.Single(List.GetVisualDescendants().OfType<AppSettingsItemRow>(),
            control => ReferenceEquals(control.ActionTarget, List.SelectedItem));
    }

    public void Type(AppInput input, string text)
    {
        input.BringIntoView();
        Flush();
        Assert.True(input.Focus(NavigationMethod.Tab));
        input.SelectAll();
        Window.KeyTextInput(text);
        Flush();
        Assert.Equal(text, input.Text);
    }

    public void AssertContained(Control control)
    {
        var origin = control.TranslatePoint(default, Window)!.Value;
        var bounds = new Rect(origin, control.Bounds.Size);
        Assert.True(new Rect(Window.ClientSize).Contains(bounds),
            $"{control.GetType().Name} bounds {bounds} exceed the {Window.ClientSize} page.");
    }

    public void AssertHit(Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)!.Value;
        var hit = Assert.IsAssignableFrom<Visual>(Window.InputHitTest(point));
        Assert.True(ReferenceEquals(control, hit) || control.IsVisualAncestorOf(hit));
    }

    public void Save(string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Flush();
        using var bitmap = Window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }

    public void Dispose()
    {
        Window.Close();
        _owner.Dispose();
    }
}
