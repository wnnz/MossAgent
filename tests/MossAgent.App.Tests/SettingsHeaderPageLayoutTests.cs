using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SettingsHeaderPageLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("providers", false, 486)]
    [InlineData("proxy", false, 486)]
    [InlineData("mcp", false, 486)]
    [InlineData("agent", false, 486)]
    [InlineData("child", false, 486)]
    [InlineData("providers", true, 486)]
    [InlineData("proxy", true, 486)]
    [InlineData("mcp", true, 486)]
    [InlineData("agent", true, 486)]
    [InlineData("child", true, 486)]
    [InlineData("providers", false, 1024)]
    [InlineData("proxy", false, 1024)]
    [InlineData("mcp", false, 1024)]
    [InlineData("agent", false, 1024)]
    [InlineData("child", false, 1024)]
    [InlineData("providers", true, 1024)]
    [InlineData("proxy", true, 1024)]
    [InlineData("mcp", true, 1024)]
    [InlineData("agent", true, 1024)]
    [InlineData("child", true, 1024)]
    public Task Header_KeepsDescriptionReadableAndRealCommandsReachable(string page, bool dark, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SettingsHeaderPageFixture(page);
        var window = new AppWindow
        {
            Width = width,
            Height = 700,
            Content = fixture.View,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            Save(window, $"{page}-{dark}-{width}");
            var header = Assert.Single(fixture.View.GetVisualDescendants().OfType<AppSettingsPageHeader>());
            Assert.Equal(page is "providers" or "proxy" or "mcp" ? new Thickness(0, 0, 0, 24) : default, header.Margin);
            var description = Assert.Single(fixture.View.GetVisualDescendants().OfType<AppText>(), text => text.Classes.Contains("settings-description"));
            var create = Assert.Single(fixture.View.GetVisualDescendants().OfType<AppActionButton>(), button => ReferenceEquals(button.Command, fixture.CreateCommand));
            Assert.True(description.TextLayout.WidthIncludingTrailingWhitespace <= description.Bounds.Width + 1,
                $"{page}: description needs {description.TextLayout.WidthIncludingTrailingWhitespace}px but receives {description.Bounds.Width}px");
            var descriptionOrigin = description.TranslatePoint(default, fixture.View)!.Value;
            var createOrigin = create.TranslatePoint(default, fixture.View)!.Value;
            Assert.True(descriptionOrigin.Y + description.Bounds.Height <= createOrigin.Y
                || descriptionOrigin.X + description.TextLayout.WidthIncludingTrailingWhitespace <= createOrigin.X);
            Assert.InRange(createOrigin.X + create.Bounds.Width, 0, fixture.View.Bounds.Width);
            Press(window, create);
            Assert.True(fixture.IsEditorOpen());
            fixture.CloseCommand.Execute(null);
            window.UpdateLayout();
            Assert.False(fixture.IsEditorOpen());
            var refresh = Assert.Single(fixture.View.GetVisualDescendants().OfType<Button>(), button => ReferenceEquals(button.Command, fixture.RefreshCommand));
            Press(window, refresh);
            await fixture.RefreshCommand.ExecutionTask!.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.False(fixture.IsEditorOpen());
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Press(AppWindow window, Button button)
    {
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus(NavigationMethod.Tab));
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        window.UpdateLayout();
    }

    private static void Save(AppWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
