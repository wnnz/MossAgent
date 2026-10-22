using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationTabEncapsulationTests
{
    [Theory]
    [InlineData("Content = new MossAgent.App.Controls.AppText()")]
    [InlineData("Padding = new Avalonia.Thickness(4)")]
    [InlineData("Width = 240")]
    [InlineData("Height = 32")]
    [InlineData("MinWidth = 240")]
    [InlineData("MaxWidth = 240")]
    [InlineData("MinHeight = 32")]
    [InlineData("MaxHeight = 32")]
    [InlineData("HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left")]
    [InlineData("ContextMenu = null")]
    public async Task BusinessCode_CannotReplaceOwnedAppearance(string assignment)
    {
        var source = "namespace MossAgent.App.Views; public class Host { public void Render() { "
            + "_ = new MossAgent.App.Controls.AppConversationTab { " + assignment + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("Width='240'", "")]
    [InlineData("ContentControl.Width='240'", "")]
    [InlineData("Padding='4'", "")]
    [InlineData("Height='32'", "")]
    [InlineData("MinWidth='240'", "")]
    [InlineData("MaxWidth='240'", "")]
    [InlineData("MinHeight='32'", "")]
    [InlineData("MaxHeight='32'", "")]
    [InlineData("", "<ui:AppText />")]
    [InlineData("", "<ui:AppConversationTab.Content><ui:AppText /></ui:AppConversationTab.Content>")]
    [InlineData("", "<ContentControl.Padding>4</ContentControl.Padding>")]
    [InlineData("", "<ui:AppConversationTab.ContextMenu><ui:AppContextMenu /></ui:AppConversationTab.ContextMenu>")]
    public void BusinessMarkup_CannotReplaceOwnedComposition(string attribute, string content) =>
        Assert.Single(Markup(attribute, content).XPathSelectElements(GuardQuery()));

    [Fact]
    public void BusinessMarkup_CanSupplySemanticStateAndCommands()
    {
        var attributes = "Title='{Binding Title}' IsSelected='{Binding IsSelected}' CommandParameter='{Binding}' "
            + "SelectCommand='{Binding Select}' CloseCommand='{Binding Close}'";
        Assert.Empty(Markup(attributes, string.Empty).XPathSelectElements(GuardQuery()));
        var view = XDocument.Load(Path.Combine(AppPath(), "Views", "WorkspaceConversationTabsView.axaml"));
        Assert.Empty(view.XPathSelectElements(GuardQuery()));
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "AppConversationTab");
        Assert.DoesNotContain(view.Descendants(), element => element.Name.LocalName is "AppFlyout" or "AppCard");
    }

    private static XDocument Markup(string attributes, string content) => XDocument.Parse(
        "<ui:AppConversationTab xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls' "
        + attributes + ">" + content + "</ui:AppConversationTab>");
    private static string GuardQuery() => Assert.IsType<string>(Assert.Single(Assert.Single(
        AppBuildDefinition.Load().Descendants("Target"), target =>
            target.Attribute("Name")?.Value == "ValidateSharedConversationTabs").Elements("XmlPeek")).Attribute("Query")?.Value);
    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
