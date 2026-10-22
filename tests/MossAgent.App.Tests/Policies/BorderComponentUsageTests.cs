using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class BorderComponentUsageTests
{
    [Fact]
    public void Axaml_BordersUseSharedAppearanceVariants()
    {
        foreach (var file in VisualFiles())
        {
            var inlineAppearance = FindInlineBorderAppearance(XDocument.Load(file));
            Assert.True(inlineAppearance.Length == 0,
                $"{Path.GetFileName(file)} overrides shared border appearance: "
                + string.Join(", ", inlineAppearance));
        }
    }

    [Fact]
    public void BorderAppearancePolicy_RejectsInlineOverridesButAllowsLayout()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppBorder Background="Red" BorderBrush="Red" BorderThickness="1"
                            CornerRadius="4" BoxShadow="0 0 4 Red" Padding="12" Width="80" />
              <ui:AppBorder Classes="attachment-chip" Padding="8" />
            </UserControl>
            """);

        Assert.Equal(["Background", "BorderBrush", "BorderThickness", "CornerRadius", "BoxShadow"],
            FindInlineBorderAppearance(document));
    }

    [Fact]
    public void BuildGuard_RejectsInlineBorderAppearance()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedBorderAppearance");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var peek = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek");
        var query = Assert.IsType<string>(peek.Attribute("Query")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("Styles/**/*.axaml", target.ToString());

        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppBorder Background="Red" BorderBrush="Red" BorderThickness="1"
                            CornerRadius="4" BoxShadow="0 0 4 Red" Padding="12" Width="80" />
              <ui:AppBorder Classes="attachment-chip" Padding="8" />
            </UserControl>
            """);
        Assert.Equal(["AppBorder"], document.XPathSelectElements(query)
            .Select(element => element.Name.LocalName));
    }

    private static string[] FindInlineBorderAppearance(XDocument document) =>
        document.Descendants()
            .Where(element => element.Name.LocalName == "AppBorder")
            .SelectMany(element => element.Attributes()
                .Where(attribute => attribute.Name.LocalName is "Background" or "BorderBrush"
                    or "BorderThickness" or "CornerRadius" or "BoxShadow")
                .Select(attribute => attribute.Name.LocalName))
            .ToArray();

    private static IEnumerable<string> VisualFiles()
    {
        var appPath = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        return Directory.EnumerateFiles(Path.Combine(appPath, "Views"), "*.axaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(appPath, "Controls"), "*.axaml", SearchOption.AllDirectories));
    }

}
