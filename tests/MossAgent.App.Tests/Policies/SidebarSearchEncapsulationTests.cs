using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SidebarSearchEncapsulationTests
{
    [Theory]
    [InlineData("Content = \"override\"")]
    [InlineData("Width = 80")]
    [InlineData("Height = 80")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    public async Task BusinessCode_CannotReplaceTheSearchComponent(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppSidebarSearch { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppSidebarSearch Width='80' />")]
    [InlineData("<ui:AppSidebarSearch Control.MinHeight='80' />")]
    [InlineData("<ui:AppSidebarSearch Padding='12' />")]
    [InlineData("<ui:AppSidebarSearch Content='override' />")]
    [InlineData("<ui:AppSidebarSearch><Control.Height>80</Control.Height></ui:AppSidebarSearch>")]
    [InlineData("<ui:AppSidebarSearch><ui:AppInput /></ui:AppSidebarSearch>")]
    public void BusinessMarkup_CannotReplaceComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void SemanticBindingsRemainAllowedAndTheSidebarNoLongerBuildsSearchFields()
    {
        Assert.Empty(Document("<ui:AppSidebarSearch Query='{Binding Query}' HasMatches='{Binding HasMatches}' ClearCommand='{Binding ClearCommand}' />")
            .XPathSelectElements(Query()));
        var sidebar = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppSidebarSearch");
        Assert.DoesNotContain(sidebar.Descendants(), element => element.Name.LocalName == "AppInput"
            && element.Attribute("PlaceholderText")?.Value == "搜索项目和对话");
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedSidebarSearch");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
