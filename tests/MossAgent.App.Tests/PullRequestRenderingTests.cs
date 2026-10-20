using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class PullRequestRenderingTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 526, 601)]
    [InlineData(true, 526, 601)]
    [InlineData(false, 980, 640)]
    [InlineData(true, 980, 640)]
    public Task GrantDialogHasVisibleFooterAndSharedControls(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var model = fixture.Main.Pages.PullRequests!;
        await model.OpenAsync();
        var view = new PullRequestsView { DataContext = model };
        var window = new Window { Content = view, Width = width, Height = height,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show();
        try
        {
            model.CreateCommand.Execute(null);
            window.UpdateLayout();
            var dialogs = view.GetVisualDescendants().OfType<AppSettingsDialog>().ToArray();
            Assert.Equal(2, dialogs.Length);
            var dialog = Assert.Single(dialogs, item => ReferenceEquals(item.DataContext, model));
            Assert.Same(dialog, Assert.Single(dialogs, item => item.IsEffectivelyVisible));
            Assert.Same(model.CloseEditorCommand, dialog.CloseCommand);
            Assert.False(model.Link.IsOpen); Assert.False(model.Link.ShowCommand.CanExecute(null));
            Assert.True(dialog.IsEffectivelyVisible);
            var save = Assert.Single(dialog.GetVisualDescendants().OfType<AppButton>(), button => button.Command == model.SaveGrantCommand);
            Assert.True(save.IsEffectivelyVisible);
            Assert.False(save.IsEffectivelyEnabled);
            var origin = save.TranslatePoint(default, window)!.Value;
            Assert.True(new Rect(window.ClientSize).Contains(new Rect(origin, save.Bounds.Size)));
            using var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable("MOSSAGENT_UI_EVIDENCE_DIRECTORY") is { Length: > 0 } folder)
            { Directory.CreateDirectory(folder); frame.Save(Path.Combine(folder, $"pr-grant-{dark}-{width}-{height}.png"), PngBitmapEncoderOptions.Default); }
            model.CloseEditorCommand.Execute(null);
            Assert.False(model.IsEditorOpen);
            model.Link.ShowCommand.Execute(null); window.UpdateLayout();
            Assert.True(model.Link.IsOpen); Assert.False(model.CreateCommand.CanExecute(null));
            Assert.Same(model.Link, Assert.Single(dialogs, item => item.IsEffectivelyVisible).DataContext);
            Assert.Equal(0, fixture.Transport.RequestCount);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
