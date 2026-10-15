using System.Xml.Linq;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppFlyoutBehaviorTests
{
    [Fact]
    public void DismissalIsOptIn()
    {
        var flyout = new AppFlyout();

        Assert.False(flyout.DismissOnButtonClick);
        flyout.DismissOnButtonClick = true;
        Assert.True(flyout.DismissOnButtonClick);
    }

    [Fact]
    public void TitlebarAndComposerActionMenusUseSharedDismissal()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var window = XDocument.Load(Path.Combine(views, "MainWindow.axaml"));
        var composer = XDocument.Load(Path.Combine(views, "..", "Controls", "AppComposerContextBar.axaml"));

        foreach (var label in new[] { "文件", "视图" })
        {
            var button = Assert.Single(window.Descendants(), element => element.Name.LocalName == "AppButton"
                && element.Attribute("Content")?.Value == label);
            Assert.Equal("True", Assert.Single(button.Descendants(), element =>
                element.Name.LocalName == "AppFlyout").Attribute("DismissOnButtonClick")?.Value);
        }

        foreach (var name in new[] { "ProjectButton", "AgentButton" })
        {
            var button = Assert.Single(composer.Descendants(), element => element.Name.LocalName is "AppButton" or "AppActionButton"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name"
                    && attribute.Value == name));
            var flyout = Assert.Single(button.Descendants(), element => element.Name.LocalName == "AppFlyout");
            Assert.Equal("True", flyout.Attribute("DismissOnButtonClick")?.Value);
            Assert.DoesNotContain(flyout.Descendants(), element => element.Attribute("Click") is not null);
        }
    }

    [Fact]
    public void InteractiveFlyoutsDoNotDismissDuringMultiStepInput()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        var flyouts = Directory.EnumerateFiles(views, "*.axaml")
            .Append(Path.Combine(views, "..", "Controls", "AppSidebarSearch.axaml"))
            .SelectMany(file => XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName == "AppFlyout"))
            .ToArray();
        var interactive = flyouts.Where(flyout => flyout.Descendants().Any(element =>
            element.Name.LocalName is "AppInput" or "ModelPickerFlyoutView")).ToArray();

        Assert.Equal(3, interactive.Length);
        Assert.All(interactive, flyout => Assert.Null(flyout.Attribute("DismissOnButtonClick")));
        Assert.DoesNotContain(flyouts.Where(flyout => flyout.Attribute("DismissOnButtonClick")?.Value == "True"),
            flyout => flyout.Descendants().Any(element => element.Name.LocalName is
                "AppInput" or "AppComboBox" or "AppSlider" or "ModelPickerFlyoutView"));
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
