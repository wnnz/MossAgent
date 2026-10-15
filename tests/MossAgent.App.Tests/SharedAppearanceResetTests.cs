using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedAppearanceResetTests
{
    [Theory]
    [InlineData("var button = new AppButton(); button.ClearValue(Button.FontSizeProperty);", "FontSize")]
    [InlineData("var button = new AppButton(); button.ClearValue(property: Button.PaddingProperty);", "Padding")]
    [InlineData("Button button = new AppButton(); button.ClearValue(Button.WidthProperty);", "Width")]
    [InlineData("var button = new AppButton(); ((Avalonia.AvaloniaObject)button).ClearValue(Button.WidthProperty);", "Width")]
    [InlineData("var button = new AppButton(); var property = Button.WidthProperty; button.ClearValue(property);", "Width")]
    [InlineData("var button = new AppIconButton(); button.ClearValue(ContentControl.ContentProperty);", "Content")]
    [InlineData("var icon = new AppIcon(); icon.ClearValue(AppIcon.SizeProperty);", "Size")]
    [InlineData("var label = new AppIconLabel(); label.ClearValue(AppIconLabel.LabelMaxWidthProperty);", "LabelMaxWidth")]
    [InlineData("var surface = new AppCard(); surface.ClearValue(Border.BoxShadowProperty);", "BoxShadow")]
    [InlineData("var flyout = new AppFlyout(); flyout.ClearValue(Flyout.FlyoutPresenterThemeProperty);", "FlyoutPresenterTheme")]
    [InlineData("var layout = new Grid(); layout.ClearValue(Grid.BackgroundProperty);", "Background")]
    public async Task BusinessCode_CannotResetSharedAppearance(string body, string property)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(ViewSource(body),
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains($"'{property}'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenericComponents_CannotResetSharedAppearance()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Reset<TComponent>(TComponent component) where TComponent : AppButton
                {
                    component.ClearValue(Button.WidthProperty);
                }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("var button = new AppButton(); button.ClearValue(Button.ContentProperty);")]
    [InlineData("var button = new AppButton(); button.ClearValue(Button.IsEnabledProperty);")]
    [InlineData("var input = new AppInput(); input.ClearValue(TextBox.TextProperty);")]
    [InlineData("var icon = new AppIconButton(); icon.ClearValue(AppIconButton.IconDataProperty);")]
    [InlineData("var layout = new Grid(); layout.ClearValue(Grid.WidthProperty);")]
    public async Task FunctionalPropertiesAndStructuralLayout_CanStillBeReset(string body)
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(ViewSource(body),
            new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task SharedComponentImplementation_CanResetOwnedAppearance()
    {
        var source = """
            using Avalonia.Controls;
            namespace MossAgent.App.Controls;
            public class SharedButton : AppButton
            {
                public void Reset()
                {
                    ClearValue(Button.WidthProperty);
                    ClearValue(Button.FontSizeProperty);
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string ViewSource(string body) => $$"""
        using Avalonia.Controls;
        using MossAgent.App.Controls;
        namespace MossAgent.App.Views;
        public class View
        {
            public void Configure() { {{body}} }
        }
        """;
}
