using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ConversationHeaderEncapsulationTests
{
    [Theory]
    [InlineData("Content = new MossAgent.App.Controls.AppText()")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    [InlineData("Width = 700")]
    [InlineData("Height = 50")]
    [InlineData("MinWidth = 500")]
    [InlineData("MaxWidth = 500")]
    [InlineData("MinHeight = 50")]
    [InlineData("MaxHeight = 50")]
    [InlineData("HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left")]
    [InlineData("VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top")]
    public async Task BusinessCode_CannotReplaceHeaderAppearance(string assignment)
    {
        var source = "namespace MossAgent.App.Views; public class Host { public void Render() { "
            + "_ = new MossAgent.App.Controls.AppConversationHeader { " + assignment + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("Width='500'")]
    [InlineData("ContentControl.Width='500'")]
    [InlineData("Padding='12'")]
    [InlineData("Height='50'")]
    [InlineData("MinWidth='500'")]
    [InlineData("MaxWidth='500'")]
    [InlineData("MinHeight='50'")]
    [InlineData("MaxHeight='50'")]
    [InlineData("HorizontalContentAlignment='Left'")]
    [InlineData("VerticalContentAlignment='Top'")]
    public void BusinessMarkup_CannotReplaceHeaderAppearance(string attribute) =>
        Assert.Single(Markup(attribute, string.Empty).XPathSelectElements(GuardQuery()));

    [Theory]
    [InlineData("<ui:AppText Text='replacement' />")]
    [InlineData("<ui:AppConversationHeader.Content><ui:AppText /></ui:AppConversationHeader.Content>")]
    [InlineData("<ContentControl.Padding>12</ContentControl.Padding>")]
    public void BusinessMarkup_CannotReplaceOwnedComposition(string content) =>
        Assert.Single(Markup(string.Empty, content).XPathSelectElements(GuardQuery()));

    [Fact]
    public void BusinessMarkup_CanBindDataCommandsAndOwnedSlots()
    {
        var document = Markup("ConversationTarget='{Binding Task}' IsRunning='{Binding IsRunning}' "
            + "Activity='{Binding Activity}' ToggleContextCommand='{Binding ToggleContext}' "
            + "ToggleInspectorCommand='{Binding ToggleInspector}'",
            "<ui:AppConversationHeader.HeadingContent><ui:AppText Text='{Binding Title}' />"
            + "</ui:AppConversationHeader.HeadingContent>"
            + "<ui:AppConversationHeader.MenuContent><ui:AppButton Command='{Binding Close}' />"
            + "</ui:AppConversationHeader.MenuContent>");
        Assert.Empty(document.XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public void Workspace_UsesHeaderWithoutRebuildingItsActions()
    {
        var view = XDocument.Load(Path.Combine(AppPath(), "Views", "WorkspaceChatView.axaml"));
        var header = Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppConversationHeader");
        Assert.Equal("{Binding Catalog.SelectedTask}", header.Attribute("ConversationTarget")?.Value);
        Assert.DoesNotContain(view.Descendants(), element => element.Attributes().Any(attribute =>
            attribute.Name.LocalName == "Name" && attribute.Value.StartsWith("ChatInspector", StringComparison.Ordinal)));
        Assert.Empty(view.XPathSelectElements(GuardQuery()));
    }

    private static XDocument Markup(string attributes, string content) => XDocument.Parse(
        "<ui:AppConversationHeader xmlns='https://github.com/avaloniaui' "
        + "xmlns:ui='clr-namespace:MossAgent.App.Controls' " + attributes + ">"
        + content + "</ui:AppConversationHeader>");

    private static string GuardQuery() => Assert.IsType<string>(Assert.Single(Assert.Single(
        AppBuildDefinition.Load().Descendants("Target"), target =>
            target.Attribute("Name")?.Value == "ValidateSharedConversationHeaders").Elements("XmlPeek"))
            .Attribute("Query")?.Value);

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
