using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerAccessibilityTests
{
    [Fact]
    public void ModelPicker_UsesNamedControlsAndSharedSelectionIcon()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var composer = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceComposerView.axaml"));
        var trigger = Assert.Single(composer.Descendants(), element =>
            element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("composer-model-picker") == true);
        Assert.Equal("{Binding ModelPicker.CurrentDisplayName}",
            trigger.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal("{Binding ModelPicker.ComposerDisplayName}", trigger.Attribute("Label")?.Value);
        Assert.Equal("{Binding ModelPicker.CurrentDisplayName}", trigger.Attribute("ToolTip.Tip")?.Value);
        Assert.Equal("{Binding ModelPicker.ReasoningDisplayName}", trigger.Attribute("BadgeText")?.Value);
        Assert.Equal("{StaticResource UiIconChevronDown}", trigger.Attribute("TrailingIconData")?.Value);

        var flyout = XDocument.Load(Path.Combine(appPath, "Views", "ModelPickerFlyoutView.axaml"));
        Assert.Equal("AppModelPickerMenu", Assert.Single(flyout.Root!.Elements()).Name.LocalName);
        var menu = XDocument.Load(Path.Combine(appPath, "Controls", "AppModelPickerMenu.axaml"));
        var option = Assert.Single(menu.Descendants(), element =>
            element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Split(' ').Contains("model-option") == true);
        Assert.Equal("{Binding AccessibleName}", option.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal("{StaticResource UiIconCheck}", option.Attribute("TrailingIconData")?.Value);
        Assert.Equal("{Binding IsSelected}", option.Attribute("IsTrailingIconVisible")?.Value);
        Assert.Equal("{Binding DisplayName}", option.Attribute("Label")?.Value);
        Assert.DoesNotContain(option.Descendants(), element =>
            element.Name.LocalName == "AppText" && element.Attribute("Text")?.Value == "✓");
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
