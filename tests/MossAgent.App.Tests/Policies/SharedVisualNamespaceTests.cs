using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedVisualNamespaceTests
{
    [Fact]
    public void BuildGuard_RejectsExternalVisualNamespaces()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualNamespaces");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("bin/**/*.axaml", target.ToString());
        Assert.Contains("obj/**/*.axaml", target.ToString());

        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls"
                         xmlns:views="clr-namespace:MossAgent.App.Views"
                         xmlns:vendor="clr-namespace:Vendor.Controls;assembly=Vendor">
              <Grid>
                <ui:AppButton />
                <views:WorkspaceSidebar />
                <vendor:Button />
                <vendor:Badge />
              </Grid>
            </UserControl>
            """);

        Assert.Equal(["Button", "Badge"], document.XPathSelectElements(GuardQuery())
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void AppMarkup_UsesOnlySharedVisualNamespaces()
    {
        var appPath = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file).Split(Path.DirectorySeparatorChar);
            if (relative[0] is "bin" or "obj") continue;

            var offenders = XDocument.Load(file).XPathSelectElements(GuardQuery())
                .Select(element => element.Name.LocalName).ToArray();
            Assert.True(offenders.Length == 0,
                $"{Path.GetFileName(file)} uses unwrapped components: {string.Join(", ", offenders)}");
        }
    }

    private static string GuardQuery()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualNamespaces");
        return Assert.IsType<string>(Assert.Single(target.Elements(), element =>
            element.Name.LocalName == "XmlPeek").Attribute("Query")?.Value);
    }

}
