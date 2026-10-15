using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedComposedControlUsageTests
{
    [Fact]
    public void BuildGuard_RejectsComposedControlOverridesButAllowsFlyouts()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedComposedControls");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("Styles/**/*.axaml", target.ToString());

        var query = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppIconLabel Spacing="3" />
              <ui:AppIconLabel><StackPanel.Spacing>4</StackPanel.Spacing></ui:AppIconLabel>
              <ui:AppIconLabel><ui:AppText Text="extra" /></ui:AppIconLabel>
              <ui:AppIconLabel ColumnSpacing="3" />
              <ui:AppIconLabel><Grid.ColumnDefinitions>*,*</Grid.ColumnDefinitions></ui:AppIconLabel>
              <ui:AppActionButton Content="override" />
              <ui:AppIconButton><Button.Content>override</Button.Content></ui:AppIconButton>
              <ui:AppSettingsNavButton><ui:AppText Text="extra" /></ui:AppSettingsNavButton>
              <ui:AppIconButton><Button.Flyout><ui:AppFlyout /></Button.Flyout></ui:AppIconButton>
              <ui:AppIconLabel Label="allowed" />
            </UserControl>
            """);

        Assert.Equal(["AppIconLabel", "AppIconLabel", "AppIconLabel", "AppIconLabel", "AppIconLabel", "AppActionButton",
            "AppIconButton", "AppSettingsNavButton"], document.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void ViewsAndControls_DoNotOverrideComposedControls()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var query = Assert.Single(project.Descendants(), element =>
                element.Name.LocalName == "Target"
                && element.Attribute("Name")?.Value == "ValidateSharedComposedControls")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);

        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var directory in new[] { "Views", "Controls" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, directory),
                "*.axaml", SearchOption.AllDirectories))
            {
                Assert.Empty(XDocument.Load(file).XPathSelectElements(query));
            }
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
