using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EditMenuLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 280)]
    [InlineData(true, 280)]
    [InlineData(false, 980)]
    [InlineData(true, 980)]
    public Task SharedMenu_AlignsLabelsAndShortcutsAtAllWindowSizes(bool dark, int width) => _session.Dispatch(() =>
    {
        var menu = new AppEditMenu();
        var input = new AppInput { Text = "alpha beta" };
        var ordinaryAction = new AppActionButton { Label = "Action", BadgeText = "Badge" };
        var window = new AppWindow
        {
            Width = width,
            Height = 380,
            Content = new StackPanel { Children = { menu, input, ordinaryAction } },
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Assert.True(input.Focus());
            input.SelectAll();
            var flyout = Assert.IsType<AppFlyout>(menu.Flyout);
            flyout.ShowAt(menu);
            window.UpdateLayout();
            var surface = Assert.IsType<AppCard>(flyout.Content);
            Assert.Equal(206, surface.Bounds.Width);
            Assert.Equal(12, surface.CornerRadius.TopLeft);
            Assert.True(surface.TryFindResource("SharedSurfaceShadow", out var shadow));
            Assert.Equal(shadow, surface.BoxShadow);
            var rows = Assert.IsType<StackPanel>(surface.Child).Children.OfType<AppActionButton>().ToArray();
            Assert.Equal(6, rows.Length);
            double? shortcutRight = null;
            double? labelLeft = null;
            foreach (var row in rows)
            {
                Assert.Equal(32, row.Bounds.Height);
                Assert.Equal(13, row.FontSize);
                Assert.Null(row.IconData);
                Assert.Equal(row.Command!.CanExecute(null), row.IsEffectivelyEnabled);
                Assert.Equal(row.IsEffectivelyEnabled ? 1 : 0.45, row.Opacity);
                var texts = row.GetVisualDescendants().OfType<AppText>().Where(text => text.IsVisible).ToArray();
                var label = Assert.Single(texts, text => text.Text == row.Label);
                var shortcut = Assert.Single(texts, text => text.Text == row.BadgeText);
                Assert.Equal(12, shortcut.FontSize);
                var labelPosition = label.TranslatePoint(default, surface)!.Value;
                var shortcutPosition = shortcut.TranslatePoint(default, surface)!.Value;
                labelLeft ??= labelPosition.X;
                shortcutRight ??= shortcutPosition.X + shortcut.Bounds.Width;
                Assert.Equal(labelLeft.Value, labelPosition.X, 3);
                Assert.Equal(shortcutRight.Value, shortcutPosition.X + shortcut.Bounds.Width, 3);
                Assert.InRange(Math.Abs(labelPosition.Y + label.Bounds.Height / 2
                    - shortcutPosition.Y - shortcut.Bounds.Height / 2), 0, 0.5);
                Assert.DoesNotContain("  ", row.Label!);
            }
            Assert.True(flyout.DismissOnButtonClick);
            Assert.Equal(10, Assert.Single(ordinaryAction.GetVisualDescendants().OfType<AppText>(),
                text => text.Classes.Contains("action-badge")).FontSize);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
