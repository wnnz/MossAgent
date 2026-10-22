using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectNavigationRowEncapsulationTests
{
    [Theory]
    [InlineData("Content = \"override\"")]
    [InlineData("Width = 80")]
    [InlineData("Height = 80")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    [InlineData("VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top")]
    public async Task BusinessCode_CannotOverrideComposition(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppProjectNavigationRow { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppProjectNavigationRow Width='80' />")]
    [InlineData("<ui:AppProjectNavigationRow Control.MinHeight='80' />")]
    [InlineData("<ui:AppProjectNavigationRow Padding='12' />")]
    [InlineData("<ui:AppProjectNavigationRow Content='override' />")]
    [InlineData("<ui:AppProjectNavigationRow><Control.Height>80</Control.Height></ui:AppProjectNavigationRow>")]
    [InlineData("<ui:AppProjectNavigationRow><ui:AppText Text='override' /></ui:AppProjectNavigationRow>")]
    public void BusinessMarkup_CannotOverrideComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void Sidebar_UsesTheSharedProjectRow()
    {
        var sidebar = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", "WorkspaceSidebar.axaml"));
        Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppProjectNavigationRow");
        Assert.DoesNotContain(sidebar.Descendants(), element => element.Attribute("Classes")?.Value.Contains("project-navigation-row") == true);
    }

    [Fact]
    public void SemanticBindingsAndSummaryContent_RemainAllowed()
    {
        var document = Document("""
            <ui:AppProjectNavigationRow Title="{Binding Project.Name}" ActionTarget="{Binding Project}"
                  IsExpanded="{Binding IsExpanded}" IsSelected="{Binding IsSelected}"
                  SelectCommand="{Binding Select}" ToggleExpandedCommand="{Binding Toggle}"
                  IsSummaryOpen="{Binding IsSummaryOpen, Mode=TwoWay}">
              <ui:AppProjectNavigationRow.SummaryContent><ui:AppCard><ui:AppText Text="{Binding Summary}" /></ui:AppCard></ui:AppProjectNavigationRow.SummaryContent>
            </ui:AppProjectNavigationRow>
            """);
        Assert.Empty(document.XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedProjectNavigationRows");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
