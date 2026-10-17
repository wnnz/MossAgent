using System.Net;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class SettingsStatusOverflowFixture : IDisposable
{
    private readonly DeferredModelDiscoveryService _discovery = new();

    public SettingsStatusOverflowFixture(bool dark, int width)
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        repository.Store.Providers[0] = repository.Store.Providers[0] with
        {
            Name = string.Concat(Enumerable.Repeat("需要保留完整名字的提供商", 120))
        };
        Providers = new ProviderSettingsViewModel(repository);
        Models = new ModelSettingsViewModel(repository, _discovery, Providers);
        Pane = new ProviderAndModelSettingsPane { DataContext = new { Providers, Models } };
        Window = new AppWindow
        {
            Content = Pane,
            Width = width,
            Height = 601,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Flush();
    }

    public ProviderSettingsViewModel Providers { get; }
    public ModelSettingsViewModel Models { get; }
    public DeferredModelDiscoveryService Discovery => _discovery;
    public ProviderAndModelSettingsPane Pane { get; }
    public AppWindow Window { get; }
    public AppSettingsPageHeader Header => Assert.Single(Pane.GetVisualDescendants().OfType<AppSettingsPageHeader>());
    public AppScrollViewer PageScroll => Assert.Single(Pane.GetVisualDescendants().OfType<AppScrollViewer>(),
        viewer => viewer.Classes.Contains("settings-page-scroll"));
    public AppActionButton Create => Assert.Single(Header.GetVisualDescendants().OfType<AppActionButton>(),
        button => ReferenceEquals(button.Command, Providers.OpenCreateFormCommand));

    public async Task FailDiscoveryAsync()
    {
        var operation = Models.FetchModelsForProviderCommand.ExecuteAsync(Assert.Single(Models.ProviderGroups));
        await _discovery.Started.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        _discovery.Fail(new HttpRequestException("synthetic-private-provider-key", null, HttpStatusCode.ServiceUnavailable));
        await operation.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Flush();
        Assert.True(Models.HasStatusError);
        Assert.DoesNotContain("synthetic-private-provider-key", Models.Status);
    }

    public void Press(Control control, Key key = Key.Space)
    {
        Assert.True(control.IsEffectivelyEnabled);
        Assert.True(control.Focus(NavigationMethod.Tab));
        Send(key);
    }

    public void Send(Key key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPress(key, modifiers, PhysicalKey.None, null);
        Window.KeyRelease(key, modifiers, PhysicalKey.None, null);
        Flush();
    }

    public void AssertOnScreen(Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), Window)!.Value;
        Assert.True(new Rect(Window.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(Window.InputHitTest(point));
        Assert.True(ReferenceEquals(control, hit) || control.IsVisualAncestorOf(hit));
    }

    public void Flush()
    {
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
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
        Models.Dispose();
        Providers.Dispose();
    }
}
