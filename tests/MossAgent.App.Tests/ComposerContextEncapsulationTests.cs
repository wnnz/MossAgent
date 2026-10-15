using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ComposerContextEncapsulationTests
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
    public async Task BusinessCode_CannotReplaceContextAppearance(string assignment)
    {
        var source = "namespace MossAgent.App.Views; public class Host { public void Render() { "
            + "_ = new MossAgent.App.Controls.AppComposerContextBar { " + assignment + " }; } }";
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
    public void BusinessMarkup_CannotReplaceContextAppearance(string attribute)
    {
        Assert.Single(Markup(attribute, string.Empty).XPathSelectElements(GuardQuery()));
    }

    [Theory]
    [InlineData("<ui:AppText Text='replacement' />")]
    [InlineData("<ui:AppComposerContextBar.Content><ui:AppText /></ui:AppComposerContextBar.Content>")]
    [InlineData("<ContentControl.Padding>12</ContentControl.Padding>")]
    public void BusinessMarkup_CannotReplaceOwnedComposition(string content)
    {
        Assert.Single(Markup(string.Empty, content).XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public void BusinessMarkup_CanBindDataCommandsAndPickerContent()
    {
        var document = Markup("Project='{Binding Project}' BranchLabel='{Binding Branch}' "
            + "Agents='{Binding Agents}' AgentName='{Binding AgentName}' HasAgents='{Binding HasAgents}' "
            + "OpenProjectCommand='{Binding Open}' SelectAgentCommand='{Binding Select}'",
            "<ui:AppComposerContextBar.ProjectPickerContent><ui:AppText Text='{Binding Title}' />"
            + "</ui:AppComposerContextBar.ProjectPickerContent>");
        Assert.Empty(document.XPathSelectElements(GuardQuery()));
    }

    private static XDocument Markup(string attributes, string content) => XDocument.Parse(
        "<ui:AppComposerContextBar xmlns='https://github.com/avaloniaui' "
        + "xmlns:ui='clr-namespace:MossAgent.App.Controls' " + attributes + ">"
        + content + "</ui:AppComposerContextBar>");

    private static string GuardQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        return Assert.IsType<string>(Assert.Single(Assert.Single(project.Descendants("Target"), target =>
            target.Attribute("Name")?.Value == "ValidateSharedWorkspacePrompts").Elements("XmlPeek"))
            .Attribute("Query")?.Value);
    }
}
