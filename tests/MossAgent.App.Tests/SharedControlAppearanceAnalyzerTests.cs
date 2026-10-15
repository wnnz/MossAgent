using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedControlAppearanceAnalyzerTests
{
    [Theory]
    [InlineData("var button = new AppButton { FontSize = 28 };")]
    [InlineData("var button = new AppButton(); button.FontSize += 2;")]
    [InlineData("var button = new AppButton(); button.FontSize++;")]
    [InlineData("var button = new AppButton(); --button.FontSize;")]
    [InlineData("var button = new AppButton(); button[Button.FontSizeProperty] = 28d;")]
    [InlineData("var button = new AppButton(); button[!Button.FontSizeProperty] = new Avalonia.Data.Binding(\"Size\");")]
    [InlineData("var button = new AppButton(); button.Padding = new Avalonia.Thickness(20);")]
    [InlineData("Button button = new AppButton(); button.Width = 80;")]
    [InlineData("var button = new AppButton(); button.SetValue(Button.FontSizeProperty, 28d);")]
    [InlineData("var button = new AppButton(); button.SetValue(value: 28d, property: Button.FontSizeProperty);")]
    [InlineData("var button = new AppButton(); ((Avalonia.AvaloniaObject)button).SetValue(Button.FontSizeProperty, 28d);")]
    [InlineData("var button = new AppButton(); ((Avalonia.AvaloniaObject)button)[Button.FontSizeProperty] = 28d;")]
    [InlineData("var button = new AppButton(); button.SetCurrentValue(Button.BackgroundProperty, Avalonia.Media.Brushes.Red);")]
    [InlineData("var button = new AppButton(); button.SetCurrentValue(value: Avalonia.Media.Brushes.Red, property: Button.BackgroundProperty);")]
    [InlineData("var button = new AppButton(); button.Bind(Button.FontSizeProperty, new Avalonia.Data.Binding(\"Size\"));")]
    [InlineData("var button = new AppButton(); button.Bind(binding: new Avalonia.Data.Binding(\"Size\"), property: Button.FontSizeProperty);")]
    [InlineData("var button = new AppButton(); Avalonia.AvaloniaObjectExtensions.Bind(button, Button.FontSizeProperty, (System.IObservable<double>)null!);")]
    [InlineData("_ = new Grid { Background = Avalonia.Media.Brushes.Red };")]
    [InlineData("_ = new AppIcon { Size = 24 };")]
    [InlineData("_ = new AppIconButton { Content = \"x\" };")]
    [InlineData("_ = new AppButton { Theme = null };")]
    [InlineData("_ = new AppButton { Template = null };")]
    [InlineData("_ = new AppInput { FocusAdorner = null };")]
    [InlineData("_ = new AppIconLabel { Spacing = 12 };")]
    [InlineData("_ = new AppIconLabel { ColumnSpacing = 12 };")]
    [InlineData("_ = new AppIconLabel { RowSpacing = 12 };")]
    [InlineData("_ = new AppIconLabel { ColumnDefinitions = new ColumnDefinitions(\"*,*\") };")]
    [InlineData("_ = new AppIconLabel { RowDefinitions = new RowDefinitions(\"*,*\") };")]
    [InlineData("_ = new AppToolActivityRow { Content = \"override\" };")]
    [InlineData("_ = new AppToolActivityRow { Height = 60 };")]
    [InlineData("_ = new AppSettingsDialog { Width = 500 };")]
    [InlineData("_ = new AppSettingsDialog { Theme = null };")]
    [InlineData("_ = new AppSettingsDialog { Padding = new Avalonia.Thickness(20) };")]
    [InlineData("var dialog = new AppSettingsDialog(); dialog.SetValue(Control.HeightProperty, 400d);")]
    [InlineData("_ = new Avalonia.Styling.Style();")]
    [InlineData("_ = new Avalonia.Styling.ControlTheme();")]
    public async Task ViewCode_CannotOverrideSharedAppearance(string body)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render() { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task SharedComponentsAndFunctionalViewProperties_AreAllowed()
    {
        var source = """
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Controls
            {
                public class SharedButton : AppButton
                {
                    public SharedButton()
                    {
                        FontSize = 14;
                        SetValue(WidthProperty, 32d);
                        _ = new Avalonia.Styling.Style();
                        FontSize += 2;
                        FontSize++;
                        this[FontSizeProperty] = 14d;
                    }
                }
            }
            namespace MossAgent.App.Views
            {
                public class View : AppWindow
                {
                    public void Render()
                    {
                        Width = 1400;
                        _ = new Grid { ColumnSpacing = 12 };
                        _ = new AppButton { Content = "Save", IsEnabled = false };
                        _ = new AppInput { Text = "value", Width = 240 };
                        _ = new AppIconButton { Label = "Close" };
                        _ = new AppColorSwatch { ColorHex = "#FFFFFF" };
                        _ = new AppSettingsDialog { IsOpen = true, Title = "Settings", Content = new AppInput() };
                        var button = new AppButton();
                        button.Classes.Add("primary");
                        button.SetValue(IsEnabledProperty, false);
                        button[IsEnabledProperty] = false;
                        button[!IsEnabledProperty] = new Avalonia.Data.Binding("CanSave");
                        button.Bind(Button.ContentProperty, new Avalonia.Data.Binding("Title"));
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }
}
