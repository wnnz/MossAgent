using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class GenericSharedControlAppearanceTests
{
    [Theory]
    [InlineData("button.FontSize = 28;")]
    [InlineData("button.FontSize += 2;")]
    [InlineData("button[Button.FontSizeProperty] = 28d;")]
    [InlineData("button.SetValue(Button.WidthProperty, 80d);")]
    [InlineData("button.Bind(Button.FontSizeProperty, new Avalonia.Data.Binding(\"Size\"));")]
    public async Task ConstrainedComponents_CannotOverrideAppearance(string body)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class Factory
            {
                public void Style<T>(T button) where T : AppButton { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task TransitiveConstraints_CannotOverrideAppearance()
    {
        var source = """
            namespace MossAgent.App.Views;
            public class Factory
            {
                public void Style<TBase, TDerived>(TDerived button)
                    where TBase : MossAgent.App.Controls.AppButton
                    where TDerived : TBase
                {
                    button.Width = 80;
                }
            }
            """;

        Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task GenericBehaviorAndLayout_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class Factory
            {
                public void Configure<T>(T button) where T : AppButton
                {
                    button.IsEnabled = false;
                    button.SetValue(value: false, property: Button.IsEnabledProperty);
                    button.Bind(binding: new Avalonia.Data.Binding("Title"), property: Button.ContentProperty);
                    Avalonia.AvaloniaObjectExtensions.Bind(button, Button.IsEnabledProperty,
                        (System.IObservable<bool>)null!);
                    button.Classes.Add("primary");
                }
                public void Layout<T>(T panel) where T : Grid
                {
                    panel.ColumnSpacing = 12;
                    panel.Width = 240;
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }
}
