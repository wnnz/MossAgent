using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestLinkRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 740)]
    [InlineData(true, 980, 740)]
    public Task LinkDialogRetainsErrorInputKeyboardEscapeAndThemeLayout(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await PullRequestLinkTestSetup.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        var view = new PullRequestsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            window.UpdateLayout();
            var opener = Assert.Single(view.GetVisualDescendants().OfType<AppButton>(), button => button.Command == model.Link.ShowCommand);
            Assert.True(opener.Focus(NavigationMethod.Tab));
            model.Link.ShowCommand.Execute(null); model.Link.Account = Assert.Single(model.Accounts);
            model.Link.Url = "https://github.com/fixture/repo/issues/7?diff=split#discussion";
            await model.Link.OpenCommand.ExecuteAsync(null); window.UpdateLayout();
            var dialog = Assert.Single(view.GetVisualDescendants().OfType<AppSettingsDialog>(), control => ReferenceEquals(control.DataContext, model.Link));
            var input = Assert.Single(dialog.GetVisualDescendants().OfType<AppInput>());
            var submit = Assert.Single(dialog.GetVisualDescendants().OfType<AppButton>(), button => button.Command == model.Link.OpenCommand);
            var result = Assert.Single(dialog.GetVisualDescendants().OfType<AppSelectableText>());
            Save(window, $"pr-link-error-{dark}-{width}-{height}");
            Assert.True(dialog.IsEffectivelyVisible); Assert.Equal(PullRequestText.Get("InvalidPrUrl"), result.Text);
            AssertInside(input, window); AssertInside(submit, window); AssertInside(result, window);
            Assert.True(input.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Escape, default, PhysicalKey.Escape, null);
            window.KeyRelease(Key.Escape, default, PhysicalKey.Escape, null); window.UpdateLayout();
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { }, Avalonia.Threading.DispatcherPriority.Background);
            Assert.False(model.Link.IsOpen); Assert.False(dialog.IsEffectivelyVisible);
            Assert.Same(opener, window.FocusManager!.GetFocusedElement());
            Assert.Contains("issues/7", model.Link.Url, StringComparison.Ordinal);
            model.Link.ShowCommand.Execute(null); model.Link.Url = "https://github.com/fixture/repo/pull/7";
            await model.Link.OpenCommand.ExecuteAsync(null); window.UpdateLayout();
            Assert.False(model.IsEmpty); Assert.NotNull(model.Detail);
            Assert.Contains(view.GetVisualDescendants().OfType<AppSelectableText>(), text => text.IsEffectivelyVisible && text.Text == model.Detail.Body);
            Save(window, $"pr-linked-detail-{dark}-{width}-{height}");
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertInside(Control control, Window window)
    {
        Assert.True(control.IsEffectivelyVisible, $"{control.GetType().Name} must be visible: {control.Bounds}");
        Assert.True(control.IsEffectivelyEnabled, $"{control.GetType().Name} must be enabled");
        var point = control.TranslatePoint(default, window)!.Value;
        Assert.InRange(point.X, 0, window.Bounds.Width - control.Bounds.Width);
        Assert.InRange(point.Y, 0, window.Bounds.Height - control.Bounds.Height);
        Assert.True(control.Bounds.Width > 0 && control.Bounds.Height > 0);
    }

    private static void Save(Window window, string name)
    {
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
        if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder); frame.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
