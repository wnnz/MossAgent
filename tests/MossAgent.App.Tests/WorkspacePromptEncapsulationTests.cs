using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePromptEncapsulationTests
{
    [Theory]
    [InlineData("new AppProjectPrompt { Content = \"override\" }")]
    [InlineData("new AppProjectPrompt { Padding = new Avalonia.Thickness(8) }")]
    [InlineData("new AppProjectPrompt { Width = 100 }")]
    [InlineData("new AppApprovalPolicyButton { Content = \"override\" }")]
    [InlineData("new AppApprovalPolicyButton { Flyout = new AppFlyout() }")]
    [InlineData("new AppApprovalPolicyButton { Command = null }")]
    [InlineData("new AppApprovalPolicyButton { CommandParameter = \"override\" }")]
    public async Task BusinessCode_CannotReplaceCompositionOrActions(string expression)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = " + expression + "; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppProjectPrompt Content='override' />")]
    [InlineData("<ui:AppProjectPrompt><ContentControl.Content>override</ContentControl.Content></ui:AppProjectPrompt>")]
    [InlineData("<ui:AppProjectPrompt Control.Width='30' />")]
    [InlineData("<ui:AppProjectPrompt><Control.Height>20</Control.Height></ui:AppProjectPrompt>")]
    [InlineData("<ui:AppProjectPrompt><ui:AppText Text='override' /></ui:AppProjectPrompt>")]
    [InlineData("<ui:AppApprovalPolicyButton Button.Flyout='{Binding Override}' />")]
    [InlineData("<ui:AppApprovalPolicyButton><Button.Command>{Binding Override}</Button.Command></ui:AppApprovalPolicyButton>")]
    [InlineData("<ui:AppApprovalPolicyButton CommandParameter='override' />")]
    [InlineData("<ui:AppApprovalPolicyButton Padding='8' />")]
    public void BusinessMarkup_CannotReplaceCompositionOrActions(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Theory]
    [InlineData("<ui:AppProjectPrompt Prefix='{Binding Prefix}' ProjectName='{Binding Name}' SelectProjectCommand='{Binding Select}'><ui:AppProjectPrompt.ProjectFlyout><ui:AppFlyout /></ui:AppProjectPrompt.ProjectFlyout></ui:AppProjectPrompt>")]
    [InlineData("<ui:AppApprovalPolicyButton Policy='{Binding Policy}' SelectPolicyCommand='{Binding Select}' IsEnabled='{Binding Available}' />")]
    public void BusinessMarkup_AllowsSemanticDataAndCommands(string markup) =>
        Assert.Empty(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void BuildGuard_CoversAllBusinessMarkupAndBothRealPages()
    {
        var appPath = Path.Combine(Root(), "src", "MossAgent.App");
        var project = XDocument.Load(Path.Combine(appPath, "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedWorkspacePrompts");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        foreach (var view in new[] { "WorkspaceChatView", "WorkspaceComposerView" })
            Assert.Empty(XDocument.Load(Path.Combine(appPath, "Views", view + ".axaml")).XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedWorkspacePrompts");
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
