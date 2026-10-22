using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ResponseContentBoundaryTests
{
    [Theory]
    [InlineData("Content")]
    [InlineData("DataTemplates")]
    [InlineData("Padding")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("MinWidth")]
    [InlineData("MaxHeight")]
    [InlineData("HorizontalContentAlignment")]
    public void Markup_RejectsResponseCompositionAndGeometryOverrides(string property)
    {
        foreach (var component in new[] { "AppResponseContent", "AppResponseTable" })
        {
            foreach (var body in new[]
            {
                $"<ui:{component} {property}=\"12\" />",
                $"<ui:{component} ui:{component}.{property}=\"12\" />",
                $"<ui:{component}><ui:{component}.{property}>12</ui:{component}.{property}></ui:{component}>",
                $"<ui:{component}><UserControl.{property}>12</UserControl.{property}></ui:{component}>"
            })
                Assert.Single(Markup(body).XPathSelectElements(GuardQuery()));
        }
    }

    [Fact]
    public void Markup_AllowsFunctionalBindingsButNotImplicitReplacementContent()
    {
        Assert.Empty(Markup("""
            <ui:AppResponseContent Blocks="{Binding Blocks}" CopyCodeCommand="{Binding Copy}" Classes="compact" />
            <ui:AppResponseTable DataContext="{Binding Table}" IsVisible="{Binding HasTable}" />
            """).XPathSelectElements(GuardQuery()));
        Assert.Single(Markup("<ui:AppResponseContent><ui:AppText Text=\"Replacement\" /></ui:AppResponseContent>")
            .XPathSelectElements(GuardQuery()));
    }

    [Theory]
    [InlineData("_ = new MossAgent.App.Controls.AppResponseContent { Content = \"replacement\" };")]
    [InlineData("_ = new MossAgent.App.Controls.AppResponseTable { Width = 40 };")]
    [InlineData("var view = new MossAgent.App.Controls.AppResponseContent(); view.ClearValue(Avalonia.Controls.ContentControl.ContentProperty);")]
    [InlineData("var view = new MossAgent.App.Controls.AppResponseTable(); view.DataTemplates.Clear();")]
    public async Task BusinessCode_RejectsCompositionAndAppearanceMutation(string body)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(Source(body),
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task BusinessCode_PreservesDataAndCommandBindings()
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source("""
            var view = new MossAgent.App.Controls.AppResponseContent();
            view.Blocks = new object[0];
            view.CopyCodeCommand = null;
            view.Classes.Add("compact");
            """), new SharedControlAppearanceAnalyzer()));
    }

    private static string Source(string body) => $$"""
        namespace MossAgent.App.Views;
        public static class Probe { public static void Create() { {{body}} } }
        """;

    private static XDocument Markup(string body) => XDocument.Parse($$"""
        <ui:AppUserControl xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
          <StackPanel>{{body}}</StackPanel>
        </ui:AppUserControl>
        """);

    private static string GuardQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var project = AppBuildDefinition.Load();
        return Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedResponseContent")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
    }
}
