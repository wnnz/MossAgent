using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Visual.Tests;

[Collection("Avalonia pixel controls")]
public sealed class SharedComponentAlignmentRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedPixelTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 640, 1)]
    [InlineData(true, 640, 1)]
    [InlineData(false, 320, 1.25)]
    [InlineData(true, 320, 1.25)]
    [InlineData(false, 640, 2)]
    [InlineData(true, 640, 2)]
    public Task FailureNotice_KeepsSpacingAndWarningAtTheFirstLine(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var notice = new AppTaskFailureNotice { Hint = string.Concat(Enumerable.Repeat("连接暂时中断，请检查网络后重试。", 6)) };
        var unrelated = new AppIcon { Classes = { "ui-icon", "icon-small", "failure-notice-icon" } };
        var host = new StackPanel { Children = { notice, unrelated } };
        var window = Show(host, dark, width, scale);
        try
        {
            Save(window, FormattableString.Invariant($"notice-{dark}-{width}-{scale}x"));
            var text = Assert.Single(notice.GetVisualDescendants().OfType<AppText>());
            var icon = Assert.Single(notice.GetVisualDescendants().OfType<AppIcon>());
            Assert.Equal(new Thickness(4, 0, 4, 16), notice.Margin);
            Assert.Equal(VerticalAlignment.Top, icon.VerticalAlignment);
            Assert.Equal(new Thickness(0, 2, 0, 0), icon.Margin);
            Assert.InRange(Origin(icon, notice).Y - Origin(text, notice).Y, 1, 3);
            Assert.True(text.TextLayout.TextLines.Count > 1);
            Assert.InRange(Origin(text, notice).X + text.Bounds.Width, 0, notice.Bounds.Width);
            Assert.Equal(new Size(12, 12), icon.Bounds.Size);
            Assert.Equal(default, unrelated.Margin);
            Assert.Equal(VerticalAlignment.Center, unrelated.VerticalAlignment);
            notice.Hint = "请检查网络后重试。";
            window.Width = width == 320 ? 640 : 320;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Save(window, FormattableString.Invariant($"notice-resized-{dark}-{width}-{scale}x"));
            Assert.Equal(notice.Hint, text.Text);
            Assert.InRange(Origin(icon, notice).Y - Origin(text, notice).Y, 1, 3);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 320, 1)]
    [InlineData(true, 320, 1)]
    [InlineData(false, 640, 1)]
    [InlineData(true, 640, 1)]
    [InlineData(false, 320, 1.25)]
    [InlineData(true, 640, 2)]
    public Task ProviderHeader_CentersTextWithActionsWithoutStylingOtherPanels(bool dark, int width, double scale) => _session.Dispatch(() =>
    {
        var provider = new AiProvider(Guid.NewGuid(), "示例提供商", ProviderProtocol.OpenAiResponses,
            new Uri("https://fixture.example.test/v1/"), string.Empty, null, true, true, new Dictionary<string, string>());
        var header = new AppProviderSettingsHeader
        {
            Provider = provider,
            ModelCount = 12,
            ToggleCommand = new RelayCommand(() => { }),
            ToggleCommandParameter = provider,
            EditCommand = new RelayCommand(() => { })
        };
        var unrelated = new StackPanel { Children = { new AppText { Text = "其他设置区域" } } };
        var window = Show(new StackPanel { Children = { header, unrelated } }, dark, width, scale);
        try
        {
            Save(window, FormattableString.Invariant($"provider-{dark}-{width}-{scale}x"));
            var name = header.FindControl<AppText>("ProviderNameText")!;
            var text = Assert.IsType<StackPanel>(name.GetVisualParent()!.GetVisualParent());
            var edit = header.FindControl<AppButton>("EditProviderButton")!;
            var toggle = header.FindControl<AppIconButton>("ToggleModelsButton")!;
            Assert.Equal(VerticalAlignment.Center, text.VerticalAlignment);
            Assert.InRange(Math.Abs(Center(text, header) - Center(edit, header)), 0, 0.5);
            Assert.InRange(Math.Abs(Center(text, header) - Center(toggle, header)), 0, 0.5);
            Assert.Equal(VerticalAlignment.Stretch, unrelated.VerticalAlignment);
            Assert.Equal(24, toggle.Bounds.Height);
            Assert.Equal(24, edit.Bounds.Height);
            window.Width = width == 320 ? 640 : 320;
            window.UpdateLayout();
            Assert.InRange(Math.Abs(Center(text, header) - Center(edit, header)), 0, 0.5);
            Assert.Same(provider, edit.CommandParameter);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow Show(Control content, bool dark, int width, double scale)
    {
        var window = new AppWindow { Width = width, Height = 340, Content = content, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        window.SetRenderScaling(scale);
        window.UpdateLayout();
        return window;
    }

    private static Point Origin(Control control, Control relative) => control.TranslatePoint(default, relative)!.Value;
    private static double Center(Control control, Control relative) => Origin(control, relative).Y + control.Bounds.Height / 2;

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
