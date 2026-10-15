using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NavigationRailEncapsulationTests
{
    [Theory]
    [InlineData("Content = \"override\"")]
    [InlineData("Width = 80")]
    [InlineData("Height = 80")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    public async Task BusinessCode_CannotReplaceTheRailComponent(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppNavigationRail { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppNavigationRail Width='80' />")]
    [InlineData("<ui:AppNavigationRail Control.MinHeight='80' />")]
    [InlineData("<ui:AppNavigationRail Padding='12' />")]
    [InlineData("<ui:AppNavigationRail Content='override' />")]
    [InlineData("<ui:AppNavigationRail><Control.Height>80</Control.Height></ui:AppNavigationRail>")]
    [InlineData("<ui:AppNavigationRail><ui:AppIconButton /></ui:AppNavigationRail>")]
    public void BusinessMarkup_CannotReplaceComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void SemanticCommandsAndHelpContentRemainAllowedWithoutBusinessButtonComposition()
    {
        Assert.Empty(Document("<ui:AppNavigationRail NewConversationCommand='{Binding NewTask}' InspectorCommand='{Binding Inspector}' "
            + "ToggleInspectorCommand='{Binding ToggleInspector}' SettingsCommand='{Binding Settings}'>"
            + "<ui:AppNavigationRail.HelpContent><ui:AppText /></ui:AppNavigationRail.HelpContent></ui:AppNavigationRail>")
            .XPathSelectElements(Query()));
        var window = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "Views", "MainWindow.axaml"));
        Assert.Single(window.Descendants(), element => element.Name.LocalName == "AppNavigationRail");
        Assert.DoesNotContain(window.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attribute("Classes")?.Value.Contains("navigation-rail-button") == true);
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedNavigationRail");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
