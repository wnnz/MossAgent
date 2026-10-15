using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AliasedSharedControlAppearanceTests
{
    [Theory]
    [InlineData("var property = Button.FontSizeProperty; button.SetValue(property, 28d);")]
    [InlineData("var property = Button.WidthProperty; var alias = property; button.SetCurrentValue(alias, 80d);")]
    [InlineData("var property = Button.FontSizeProperty; button.Bind(property, new Avalonia.Data.Binding(\"Size\"));")]
    [InlineData("var property = Button.FontSizeProperty; button[property] = 28d;")]
    [InlineData("var property = !Button.FontSizeProperty; button[property] = new Avalonia.Data.Binding(\"Size\");")]
    [InlineData("Avalonia.AvaloniaObject target = button; target.SetValue(Button.WidthProperty, 80d);")]
    [InlineData("Avalonia.AvaloniaObject target = button; var alias = target; alias[Button.FontSizeProperty] = 28d;")]
    [InlineData("Avalonia.AvaloniaObject target = button; var property = Button.FontSizeProperty; target.Bind(property, new Avalonia.Data.Binding(\"Size\"));")]
    [InlineData("var property = Button.FontSizeProperty; Avalonia.AvaloniaObjectExtensions.Bind(button, property, (System.IObservable<double>)null!);")]
    [InlineData("var styles = button.Styles; styles.Add(style);")]
    [InlineData("var styles = button.Styles; styles.Clear();")]
    [InlineData("var styles = button.Styles; styles[0] = style;")]
    [InlineData("var styles = button.Styles; var alias = styles; alias.Insert(0, style);")]
    [InlineData("System.Collections.Generic.ICollection<Avalonia.Styling.IStyle> styles = button.Styles; styles.Add(style);")]
    public async Task StableLocalAliases_CannotBypassSharedAppearance(string body)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render(AppButton button, Avalonia.Styling.IStyle style) { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task FunctionalAliasesAndReadOnlyInspection_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render(AppButton button, AppWindow window)
                {
                    var property = Button.IsEnabledProperty;
                    button.SetValue(property, false);
                    button.Bind(property, new Avalonia.Data.Binding("Enabled"));
                    button[!property] = new Avalonia.Data.Binding("Enabled");
                    Avalonia.AvaloniaObject target = button;
                    target.SetValue(Button.ContentProperty, "Save");
                    Avalonia.AvaloniaObject host = window;
                    host.SetValue(Control.WidthProperty, 1400d);
                    var styles = button.Styles;
                    var count = styles.Count;
                    var font = Button.FontSizeProperty;
                    var size = button.GetValue(font);
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task ReassignedAliases_AreNotMistakenForTheirInitialValue()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render(AppButton button, AppWindow window)
                {
                    Avalonia.AvaloniaObject target = button;
                    target = window;
                    target.SetValue(Control.WidthProperty, 1400d);
                    var property = Button.FontSizeProperty;
                    property = Control.WidthProperty;
                    window.SetValue(property, 1400d);
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task SharedComponentAndApplicationAliases_RemainAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedButton : AppButton
                {
                    public void Configure(Avalonia.Styling.IStyle style)
                    {
                        var property = Button.FontSizeProperty;
                        SetValue(property, 14d);
                        var styles = Styles;
                        styles.Add(style);
                    }
                }
            }
            namespace MossAgent.App
            {
                public class App : Avalonia.Application
                {
                    public void Configure(Avalonia.Styling.IStyle style)
                    {
                        var styles = Styles;
                        styles.Add(style);
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }
}
