using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerSettingsPaneLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(280, false)]
    [InlineData(280, true)]
    [InlineData(720, false)]
    [InlineData(720, true)]
    public Task PaneFitsWidthAndCapturesFrame(int width, bool dark) => _session.DispatchAsync(async () =>
    {
        var store = new ComposerPreferenceStoreProbe { Current = ComposerRunningAction.Steer };
        using var preferences = new WorkspaceComposerPreferencesViewModel(store,
            new CountingProbeCommand(), new CountingProbeCommand());
        await preferences.LoadAsync();
        var pane = new ComposerSettingsPane { DataContext = preferences };
        var window = new AppWindow { Width = width, Height = 480, Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, ComposerPreferenceText.Save));
            Assert.True(save.IsEffectivelyVisible);
            var combo = Assert.Single(pane.GetVisualDescendants().OfType<AppComboBox>());
            var end = save.TranslatePoint(new Point(save.Bounds.Width, save.Bounds.Height), pane)!.Value;
            Assert.InRange(end.X, 0, width);
            Assert.InRange(end.Y, 0, 480);
            var comboEnd = combo.TranslatePoint(new Point(combo.Bounds.Width, 0), pane)!.Value;
            Assert.InRange(comboEnd.X, 0, width);
            if (Environment.GetEnvironmentVariable("MOSSAGENT_TEST_SCREENSHOTS") is { Length: > 0 } output)
            {
                Directory.CreateDirectory(output);
                using var frame = window.CaptureRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(output, $"composer-settings-{(dark ? "dark" : "light")}-{width}.png"), PngBitmapEncoderOptions.Default);
            }
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
