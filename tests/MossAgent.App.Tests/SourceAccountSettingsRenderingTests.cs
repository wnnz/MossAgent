using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SourceAccountSettingsRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601, 1)]
    [InlineData(true, 526, 601, 1)]
    [InlineData(false, 980, 640, 1)]
    [InlineData(true, 980, 640, 1)]
    [InlineData(false, 980, 640, 1.25)]
    [InlineData(true, 980, 640, 1.25)]
    public Task DialogFooterAndKeyboardEscapeWorkAcrossSizesAndThemes(bool dark, int width, int height, double scale) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await SourceAccountUiFixture.CreateAsync();
            var view = new SourceAccountSettingsPane { DataContext = fixture.Model };
            var window = new Window { Content = view, Width = width, Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                window.SetRenderScaling(scale);
                window.UpdateLayout();
                SaveEvidence(window, $"source-{dark}-{width}-{height}-{scale}-empty");
                fixture.Model.OpenCreateFormCommand.Execute(null);
                fixture.Model.Editor.Name = "GitHub 工作账号";
                fixture.Model.Editor.AccessToken = "synthetic-render-source-credential";
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var dialog = Assert.Single(view.GetVisualDescendants().OfType<AppSettingsDialog>());
                var buttons = dialog.GetVisualDescendants().OfType<AppButton>()
                    .Where(button => button.Command == fixture.Model.SaveCommand || button.Command == fixture.Model.CloseFormCommand).ToArray();
                Assert.NotEmpty(buttons);
                foreach (var button in buttons)
                {
                    Assert.True(button.IsEffectivelyVisible);
                    var origin = button.TranslatePoint(default, window)!.Value;
                    Assert.True(new Rect(window.ClientSize).Contains(new Rect(origin, button.Bounds.Size)));
                }
                SaveEvidence(window, $"source-{dark}-{width}-{height}-{scale}-dialog");
                var token = Assert.Single(dialog.GetVisualDescendants().OfType<AppInput>(), input => input.PasswordChar == '●');
                Assert.True(token.Focus(NavigationMethod.Tab));
                token.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
                Assert.False(fixture.Model.Editor.IsOpen);
                Assert.Empty(fixture.Model.Editor.AccessToken);
                Assert.Equal(0, fixture.Transport.RequestCount);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void SaveEvidence(Window window, string name)
    {
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        Assert.Equal(new PixelSize((int)(window.Width * window.RenderScaling), (int)(window.Height * window.RenderScaling)), bitmap.PixelSize);
        if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is not { Length: > 0 } folder) return;
        Directory.CreateDirectory(folder);
        bitmap.Save(Path.Combine(folder, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
