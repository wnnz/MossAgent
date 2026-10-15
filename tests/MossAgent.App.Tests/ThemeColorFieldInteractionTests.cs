using Avalonia;
using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemeColorFieldInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 384)]
    [InlineData(true, 384)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task ColorPillsContainTheirSwatchAndEditorAtMeasuredSize(bool dark, double width) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            fixture.Main.SelectThemeCommand.Execute(dark ? "Dark" : "Light");
            var palette = new AppThemePaletteCard { DataContext = fixture.Main.ActivePalette };
            var window = new AppWindow { Width = width, Height = 280, Content = palette };
            window.Show();
            try
            {
                Flush(window);
                var fields = palette.GetVisualDescendants().OfType<AppUserControl>()
                    .Where(control => control.Classes.Contains("app-theme-color-input")).ToArray();
                Assert.Equal(2, fields.Length);
                Assert.All(fields, field =>
                {
                    Assert.Equal(new Size(96, 28), field.Bounds.Size);
                    var input = field.GetVisualDescendants().OfType<AppInput>().Single();
                    var swatch = field.GetVisualDescendants().OfType<AppColorSwatch>().Single();
                    Assert.Equal(new Size(14, 14), swatch.Bounds.Size);
                    Assert.Equal(input.Text, swatch.ColorHex);
                    Assert.InRange(input.TranslatePoint(default, field)!.Value.Y, 0, 2);
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(input)));
                    Assert.InRange(field.TranslatePoint(default, palette)!.Value.X + field.Bounds.Width, width - 18, width - 16);
                });
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardEditingUpdatesTheDraftAndRejectsInvalidSaveWithoutLosingFocus(bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            fixture.Main.SelectThemeCommand.Execute(dark ? "Dark" : "Light");
            var editor = fixture.Main.ActivePalette;
            var palette = new AppThemePaletteCard { DataContext = editor };
            var window = new AppWindow { Width = 384, Height = 280, Content = palette };
            window.Show();
            try
            {
                Flush(window);
                var inputs = palette.GetVisualDescendants().OfType<AppInput>().ToArray();
                var background = Assert.Single(inputs, input => AutomationProperties.GetName(input) == "主题背景色");
                var foreground = Assert.Single(inputs, input => AutomationProperties.GetName(input) == "主题前景色");
                Assert.True(background.Focus());
                window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                window.KeyTextInput("#invalid");
                Flush(window);
                Assert.Equal("#invalid", editor.BackgroundHex);
                editor.SaveCommand.Execute(null);
                Assert.True(editor.HasError);
                Assert.Same(background, window.FocusManager!.GetFocusedElement());
                window.KeyPress(Key.A, RawInputModifiers.Control, PhysicalKey.A, null);
                window.KeyTextInput(dark ? "#181818" : "#FFFFFF");
                Flush(window);
                editor.SaveCommand.Execute(null);
                Assert.False(editor.HasError);
                window.KeyPress(Key.Tab, RawInputModifiers.None, PhysicalKey.Tab, null);
                Flush(window);
                Assert.Same(foreground, window.FocusManager.GetFocusedElement());
                foreground.IsEnabled = false;
                Assert.False(foreground.Focus());
                foreground.IsEnabled = true;
                Assert.True(foreground.Focus());
                Assert.Empty(fixture.Server.Requests);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
