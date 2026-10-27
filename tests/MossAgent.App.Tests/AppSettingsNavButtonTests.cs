using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppSettingsNavButtonTests
{
    [Fact]
    public void NavigationButton_AlignsIconAndSelectedText()
    {
        var button = new AppSettingsNavButton { Label = "外观" };
        var layout = Assert.IsType<AppIconLabel>(button.Content);
        Assert.Equal(GridLength.Auto, layout.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Star, layout.ColumnDefinitions[1].Width);
        Assert.Contains("owned-icon-label", layout.Classes);
        Assert.Collection(layout.Children,
            child => Assert.Equal(VerticalAlignment.Center,
                Assert.IsType<AppIcon>(child).VerticalAlignment),
            child => Assert.Equal(VerticalAlignment.Center,
                Assert.IsType<AppText>(child).VerticalAlignment));

        button.FontSize = 14;
        button.FontWeight = FontWeight.SemiBold;
        var label = Assert.IsType<AppText>(layout.Children[1]);
        Assert.Equal(14, label.FontSize);
        Assert.Equal(FontWeight.SemiBold, label.FontWeight);
        Assert.Equal("外观", AutomationProperties.GetName(button));
    }

    [Fact]
    public void SettingsOverlay_UsesSharedNavigationComponent()
    {
        var appDirectory = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var document = XDocument.Load(Path.Combine(appDirectory,
            "Views", "SettingsOverlay.axaml"));
        var app = XDocument.Load(Path.Combine(appDirectory, "App.axaml"));
        var navStyle = Assert.Single(app.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == ".app-toggle-button.settings-nav");
        Assert.Contains(navStyle.Elements(), element =>
            element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Theme"
            && element.Attribute("Value")?.Value == "{StaticResource SettingsNavTheme}");
        var buttons = document.Descendants()
            .Where(element => element.Name.LocalName == "AppSettingsNavButton")
            .ToArray();

        Assert.Equal(new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" },
            buttons.Select(button => button.Attribute("CommandParameter")?.Value).Order(StringComparer.Ordinal));
        Assert.Contains(buttons, button => button.Attribute("Label")?.Value == "{x:Static labels:SourceAccountText.Title}");
        Assert.DoesNotContain(document.Descendants(), element =>
            element.Name.LocalName == "AppToggleButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("settings-nav") == true);
        foreach (var button in buttons)
        {
            Assert.NotNull(button.Attribute("Label"));
            Assert.NotNull(button.Attribute("IconData"));
            Assert.Null(button.Attribute("Theme"));
            Assert.NotNull(button.Attribute("Command"));
            Assert.NotNull(button.Attribute("IsChecked"));
            Assert.Empty(button.Elements());
        }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
