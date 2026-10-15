using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspacePaneEncapsulationTests
{
    [Theory]
    [InlineData("new AppWorkspacePanes { Content = null }")]
    [InlineData("new AppWorkspacePanes { Width = 300 }")]
    [InlineData("new AppWorkspacePanes { MinimumPrimaryWidth = 0 }")]
    [InlineData("new AppWorkspacePanes { DefaultInspectorFraction = 0.9 }")]
    [InlineData("new AppWorkspacePanes { FloatingInspectorMargin = default }")]
    [InlineData("new AppPaneSplitter { Width = 20 }")]
    [InlineData("new AppPaneSplitter { ResizeDirection = Avalonia.Controls.GridResizeDirection.Rows }")]
    [InlineData("new AppPaneSplitter { KeyboardIncrement = 100 }")]
    public async Task BusinessCode_CannotOverrideWorkspaceGeometry(string expression)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = " + expression + "; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task BusinessCode_CanComposeSharedPanesAndChangeVisibility()
    {
        const string source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { var panes = new AppWorkspacePanes { PrimaryPane = new AppBorder(), "
            + "InspectorPane = new AppBorder(), IsInspectorOpen = true, IsInspectorExpanded = true }; panes.IsInspectorOpen = false; } }";
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    [Theory]
    [InlineData("<ui:AppWorkspacePanes Width='300' />")]
    [InlineData("<ui:AppWorkspacePanes><ContentControl.Content>override</ContentControl.Content></ui:AppWorkspacePanes>")]
    [InlineData("<ui:AppWorkspacePanes Control.MinWidth='0' />")]
    [InlineData("<ui:AppWorkspacePanes><ui:AppWorkspacePanes.DefaultInspectorFraction>0.9</ui:AppWorkspacePanes.DefaultInspectorFraction></ui:AppWorkspacePanes>")]
    [InlineData("<ui:AppWorkspacePanes><Grid /></ui:AppWorkspacePanes>")]
    [InlineData("<ui:AppPaneSplitter ResizeDirection='Rows' />")]
    [InlineData("<ui:AppPaneSplitter><GridSplitter.ResizeBehavior>CurrentAndNext</GridSplitter.ResizeBehavior></ui:AppPaneSplitter>")]
    [InlineData("<ui:AppPaneSplitter Control.Width='20' />")]
    public void BusinessMarkup_CannotOverrideWorkspaceGeometry(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void MainWindow_BindsSharedPanesWithoutReplacingTheirLayout()
    {
        var path = Path.Combine(Root(), "src", "MossAgent.App", "Views", "MainWindow.axaml");
        var document = XDocument.Load(path);
        var panes = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppWorkspacePanes");
        Assert.Equal("{Binding IsRightPanelOpen, Mode=TwoWay}", panes.Attribute("IsInspectorOpen")?.Value);
        Assert.Single(panes.Descendants(), element => element.Name.LocalName == "WorkspaceChatView");
        Assert.Single(panes.Descendants(), element => element.Name.LocalName == "InspectorPane");
        Assert.Empty(document.XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedWorkspacePanes");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
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
