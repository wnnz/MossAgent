using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TaskFailureNoticeEncapsulationTests
{
    [Theory]
    [InlineData("Content = \"override\"")]
    [InlineData("Width = 32")]
    [InlineData("Height = 32")]
    [InlineData("Padding = new Avalonia.Thickness(16)")]
    public async Task BusinessCode_CannotReplaceNoticeComposition(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppTaskFailureNotice { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppTaskFailureNotice Width='32' />")]
    [InlineData("<ui:AppTaskFailureNotice Control.Height='32' />")]
    [InlineData("<ui:AppTaskFailureNotice><ContentControl.Content>override</ContentControl.Content></ui:AppTaskFailureNotice>")]
    [InlineData("<ui:AppTaskFailureNotice><ui:AppText Text='override' /></ui:AppTaskFailureNotice>")]
    public void BusinessMarkup_CannotReplaceNoticeComposition(string markup)
    {
        Assert.Single(Document(markup).XPathSelectElements(Query()));
    }

    [Fact]
    public void WorkspaceUsesOwnedWrappingNoticeWithFunctionalBindings()
    {
        var root = AppBuildDefinition.RepositoryRoot;
        var document = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceChatView.axaml"));
        var notice = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppTaskFailureNotice");
        Assert.Equal("{Binding TaskFailureHint}", notice.Attribute("Hint")?.Value);
        Assert.Equal("{Binding IsSelectedTaskFailed}", notice.Attribute("IsVisible")?.Value);
        Assert.Empty(Document("<ui:AppTaskFailureNotice Hint='{Binding Hint}' IsVisible='{Binding Failed}' />").XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedTaskFailureNotices");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
