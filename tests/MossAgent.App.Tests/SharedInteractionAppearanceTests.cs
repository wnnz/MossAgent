using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedInteractionAppearanceTests
{
    [Fact]
    public void ComboBoxes_UseSharedPopupAndNamedBrowserFields()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "ComboBoxVariants.axaml"));
        var popup = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value == ".app-combo-box /template/ Border#PopupBorder");
        Assert.Contains(popup.Elements(), element => element.Attribute("Property")?.Value == "CornerRadius"
            && element.Attribute("Value")?.Value == "8");
        var selected = Assert.Single(styles.Descendants(), element =>
            element.Attribute("Selector")?.Value == "ComboBoxItem:selected /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains(selected.Elements(), element => element.Attribute("Property")?.Value == "Background"
            && element.Attribute("Value")?.Value == "{DynamicResource SettingsSelectionBrush}");

        var browser = XDocument.Load(Path.Combine(appPath, "Views", "BrowserSettingsPane.axaml"));
        var fields = browser.Descendants().Where(element =>
            element.Name.LocalName is "AppComboBox" or "AppInput").ToArray();
        Assert.Equal(5, fields.Length);
        Assert.All(fields, field => Assert.False(string.IsNullOrWhiteSpace(
            field.Attribute("AutomationProperties.Name")?.Value)));
    }

    [Fact]
    public void SettingsEntryAndThemeChoices_ExposeAccessibleNames()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var window = XDocument.Load(Path.Combine(views, "MainWindow.axaml"));
        var appearance = XDocument.Load(Path.Combine(views, "..", "Controls", "AppThemeModePicker.axaml"));
        var settings = XDocument.Load(Path.Combine(views, "SettingsOverlay.axaml"));
        var rail = Assert.Single(window.Descendants(), element => element.Name.LocalName == "AppNavigationRail"
            && element.Attribute("Grid.Column")?.Value == "0");
        Assert.Equal("{Binding ToggleSettingsCommand}", rail.Attribute("SettingsCommand")?.Value);
        var component = XDocument.Load(Path.Combine(views, "..", "Controls", "AppNavigationRail.axaml"));
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Label")?.Value == "{Binding SettingsLabel}"
            && element.Attribute("Command")?.Value == "{Binding SettingsCommand}"
            && element.Attribute("IconData")?.Value == "{DynamicResource UiIconSettings}");
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Label")?.Value == "{Binding HelpLabel}");
        Assert.Equal(["系统主题", "浅色主题", "深色主题"], appearance.Descendants()
            .Where(element => element.Name.LocalName == "AppButton"
                && element.Attribute("Classes")?.Value.Contains("theme-mode-button") == true)
            .Select(element => LocalizableText.Unwrap(element.Attribute("AutomationProperties.Name")?.Value)));
        Assert.Contains(settings.Descendants(), element => element.Name.LocalName == "AppInput"
            && LocalizableText.Unwrap(element.Attribute("AutomationProperties.Name")?.Value) == "搜索设置");
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
