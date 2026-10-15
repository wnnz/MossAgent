using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ChoiceCornerRadiusTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SelectorsAndPopups_UseSmallRectangularCornersInEveryState(bool dark) => _session.Dispatch(() =>
    {
        AppComboBox[] selectors =
        [
            new AppComboBox(),
            new AppChoiceComboBox { Choices = ["low", "medium", "high"] },
            new AppPillComboBox(),
            new AppComboBox { Classes = { "compact-selector" } },
            new AppComboBox { Classes = { "browser-tabs" } }
        ];
        foreach (var selector in selectors)
        {
            Popup? popup = null;
            selector.TemplateApplied += (_, args) => popup = args.NameScope.Find<Popup>("PART_Popup");
            if (selector is not AppChoiceComboBox) selector.ItemsSource = new[] { "low", "medium", "high" };
            selector.SelectedIndex = 1;
            var window = CreateWindow(selector, dark);
            window.Show();
            try
            {
                window.UpdateLayout();
                var size = selector.Bounds.Size;
                AssertSelectorCorners(selector);
                Assert.True(selector.Focus(NavigationMethod.Tab));
                window.UpdateLayout();
                AssertSelectorCorners(selector);
                selector.IsDropDownOpen = true;
                window.UpdateLayout();
                var surface = Assert.IsAssignableFrom<Border>(Assert.IsAssignableFrom<Popup>(popup).Child);
                Assert.Equal(new CornerRadius(8), surface.CornerRadius);
                Assert.Equal(size, selector.Bounds.Size);
                selector.IsDropDownOpen = false;
                selector.IsEnabled = false;
                window.UpdateLayout();
                AssertSelectorCorners(selector);
                Assert.Equal(size, selector.Bounds.Size);
            }
            finally { window.Close(); }
        }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task EditableChoice_UsesTheSameSmallCornersWithoutChangingOtherInputs(bool dark) => _session.Dispatch(() =>
    {
        var choice = new AppEditableChoice { Choices = ["low", "high"], SelectedChoice = "high" };
        var input = new AppInput();
        var window = CreateWindow(new StackPanel { Children = { choice, input } }, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            var editor = Assert.Single(choice.GetVisualDescendants().OfType<AppInput>());
            Assert.Equal(new CornerRadius(8), editor.CornerRadius);
            var border = Assert.Single(editor.GetVisualDescendants().OfType<Border>(),
                element => element.Name == "PART_BorderElement");
            Assert.Equal(new CornerRadius(8), border.CornerRadius);
            Assert.Equal(new CornerRadius(12), input.CornerRadius);
            Assert.Equal("high", editor.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppWindow CreateWindow(Control content, bool dark) => new()
    {
        Width = 300,
        Height = 180,
        Content = content,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void AssertSelectorCorners(AppComboBox selector)
    {
        Assert.Equal(new CornerRadius(8), selector.CornerRadius);
        var surfaces = selector.GetVisualDescendants().OfType<Border>()
            .Where(border => border.Name is "Background" or "HighlightBackground").ToArray();
        Assert.NotEmpty(surfaces);
        Assert.All(surfaces, surface => Assert.Equal(new CornerRadius(8), surface.CornerRadius));
    }
}
