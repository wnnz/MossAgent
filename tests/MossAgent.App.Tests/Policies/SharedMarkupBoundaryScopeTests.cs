using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedMarkupBoundaryScopeTests
{
    [Theory]
    [InlineData("ValidateSharedVisualTemplates", "_TemplateBoundaryMarkup")]
    [InlineData("ValidateSharedSettingsDialogs", "_DialogAppearanceMarkup")]
    [InlineData("ValidateSharedComposedControls", "_ComposedMarkup")]
    [InlineData("ValidateNoViewLocalStyles", "_ViewMarkup")]
    [InlineData("ValidateLayoutPrimitivesRemainStructural", "_LayoutMarkup")]
    [InlineData("ValidateSharedAppearanceTokens", "_AppearanceTokenMarkup")]
    [InlineData("ValidateSharedComponentAppearance", "_AppearanceMarkup")]
    [InlineData("ValidateSharedFieldSizing", "_FieldSizingMarkup")]
    [InlineData("ValidateSharedBorderAppearance", "_BorderMarkup")]
    [InlineData("ValidateSharedPropertyElements", "_PropertyMarkup")]
    [InlineData("ValidateInheritedAppearancePropertyElements", "_InheritedPropertyMarkup")]
    public void AppearanceGuards_CoverNewMarkupDirectories(string targetName, string itemName)
    {
        var target = Target(targetName);
        var item = Assert.Single(target.Descendants(itemName));

        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Equal("$(MSBuildProjectDirectory)/**/*.axaml", item.Attribute("Include")?.Value);
        Assert.Equal("$(MSBuildProjectDirectory)/App.axaml;$(MSBuildProjectDirectory)/Styles/**/*.axaml;"
            + "$(MSBuildProjectDirectory)/bin/**/*.axaml;$(MSBuildProjectDirectory)/obj/**/*.axaml",
            item.Attribute("Exclude")?.Value);
    }

    [Theory]
    [InlineData("<ui:AppEmbeddedBrowserView Foreground='Red' />", "AppEmbeddedBrowserView")]
    [InlineData("<ui:FutureComponent FontSize='28' />", "FutureComponent")]
    [InlineData("<ui:AppUserControl Background='Red' />", "AppUserControl")]
    [InlineData("<ui:AppButton TextElement.FontFamily='Arial' />", "AppButton")]
    [InlineData("<ui:AppInput><TextElement.Foreground>Red</TextElement.Foreground></ui:AppInput>", "TextElement.Foreground")]
    public void AppearanceTokens_AreRejectedRegardlessOfComponentName(string markup, string offender)
    {
        var document = XDocument.Parse("<Grid xmlns='https://github.com/avaloniaui' "
            + "xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

        Assert.Equal(offender, Assert.Single(document.XPathSelectElements(TokenQuery())).Name.LocalName);
    }

    [Fact]
    public void CurrentBusinessMarkup_UsesSharedAppearanceTokens()
    {
        var appPath = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file);
            if (relative == "App.axaml" || relative.Split(Path.DirectorySeparatorChar)[0] is "Styles" or "bin" or "obj")
                continue;

            Assert.True(!XDocument.Load(file).XPathSelectElements(TokenQuery()).Any(),
                $"{relative} overrides shared appearance tokens.");
        }
    }

    [Fact]
    public void FunctionalPropertiesAndStructuralLayout_RemainAllowed()
    {
        var document = XDocument.Parse("""
            <Grid xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls"
                  Margin="8" ColumnSpacing="12" Width="240">
              <ui:AppInput Text="value" Width="200" />
              <ui:AppButton Content="Save" Classes="primary" IsEnabled="False" />
              <ui:AppIconButton Label="Close" IconData="{StaticResource UiIconClose}" />
              <ui:AppColorSwatch ColorHex="#FFFFFF" />
            </Grid>
            """);

        Assert.Empty(document.XPathSelectElements(TokenQuery()));
    }

    private static string TokenQuery() => Assert.IsType<string>(
        Assert.Single(Target("ValidateSharedAppearanceTokens").Elements("XmlPeek")).Attribute("Query")?.Value);

    private static XElement Target(string name) => Assert.Single(AppBuildDefinition.Load().Descendants("Target"),
        target => target.Attribute("Name")?.Value == name);

}
