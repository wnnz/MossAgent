using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SettingsScrollComponentTests
{
    [Fact]
    public void SettingsPages_UseSharedScrollVariant()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var page in new[]
        {
            "AppearanceSettingsPane", "AgentSettingsPane", "BrowserSettingsPane",
            "SubagentSettingsPane", "ProviderAndModelSettingsPane"
        })
        {
            var document = XDocument.Load(Path.Combine(appPath, "Views", $"{page}.axaml"));
            var pageScroll = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "AppScrollViewer"
                && element.Attribute("Classes")?.Value.Split(' ').Contains("settings-page-scroll") == true);

            Assert.Null(pageScroll.Attribute("HorizontalScrollBarVisibility"));
            Assert.Null(pageScroll.Attribute("VerticalScrollBarVisibility"));
        }

        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var variant = Assert.Single(styles.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == ".app-scroll-viewer.settings-page-scroll");
        var setters = variant.Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => element.Attribute("Property")!.Value,
                element => element.Attribute("Value")!.Value);

        Assert.Equal("Disabled", setters["HorizontalScrollBarVisibility"]);
        Assert.Equal("Hidden", setters["VerticalScrollBarVisibility"]);
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
