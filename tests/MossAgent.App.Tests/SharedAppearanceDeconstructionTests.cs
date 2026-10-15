using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedAppearanceDeconstructionTests
{
    [Theory]
    [InlineData("(button.Width, enabled) = (22d, false);", "Width")]
    [InlineData("(button.FontSize, enabled) = (28d, false);", "FontSize")]
    [InlineData("(enabled, (button.Height, visible)) = (false, (22d, true));", "Height")]
    [InlineData("(button[Button.WidthProperty], enabled) = (22d, false);", "Width")]
    [InlineData("(((Button)button).Padding, enabled) = (new Avalonia.Thickness(9), false);", "Padding")]
    [InlineData("(icon.Size, enabled) = (24d, false);", "Size")]
    [InlineData("(input.MinHeight, enabled) = (80d, false);", "MinHeight")]
    [InlineData("(row.Content, enabled) = (\"override\", false);", "Content")]
    [InlineData("(button.Classes[0], enabled) = (\"replacement\", false);", "Classes")]
    [InlineData("(button.Resources[\"Brush\"], enabled) = (Avalonia.Media.Brushes.Red, false);", "Resources")]
    [InlineData("(layout.Background, enabled) = (Avalonia.Media.Brushes.Red, false);", "Background")]
    [InlineData("(button.Foreground, _) = (Avalonia.Media.Brushes.Red, false);", "Foreground")]
    public async Task ViewCode_CannotOverrideAppearanceThroughDeconstruction(string body, string property)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(Source(body),
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains($"'{property}'", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NestedDeconstruction_ReportsEveryAppearanceOverride()
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze(Source("""
            (button.Width, (button.Height, icon.Size)) = (22d, (22d, 24d));
            """), new SharedControlAppearanceAnalyzer());

        Assert.Equal(3, diagnostics.Length);
        Assert.All(diagnostics, diagnostic => Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId,
            diagnostic.Id));
        foreach (var property in new[] { "Width", "Height", "Size" })
            Assert.Contains(diagnostics, diagnostic => diagnostic.GetMessage().Contains($"'{property}'",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("(button.IsEnabled, button.IsVisible) = (false, true);")]
    [InlineData("(input.Text, input.IsReadOnly) = (\"draft\", true);")]
    [InlineData("(layout.Width, layout.Height) = (320d, 240d);")]
    [InlineData("(enabled, (visible, _)) = (false, (true, 42));")]
    public async Task FunctionalAndStructuralDeconstruction_RemainsAllowed(string body)
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source(body),
            new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task SharedComponentImplementation_CanConfigureItsOwnAppearance()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls;
            public class SharedButton : AppButton
            {
                public void Configure() { (Width, Height) = (32d, 32d); }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string Source(string body) => $$"""
        using Avalonia.Controls;
        using MossAgent.App.Controls;
        namespace MossAgent.App.Views;
        public class View
        {
            public void Render(AppButton button, AppIcon icon, AppInput input,
                AppTaskNavigationRow row, Grid layout)
            {
                bool enabled = true, visible = false;
                {{body}}
            }
        }
        """;
}
