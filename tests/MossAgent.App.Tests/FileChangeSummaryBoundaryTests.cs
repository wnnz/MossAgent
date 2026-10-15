using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class FileChangeSummaryBoundaryTests
{
    [Theory]
    [InlineData("Content", "new AppText()")]
    [InlineData("Padding", "new Avalonia.Thickness(10)")]
    [InlineData("Width", "400d")]
    [InlineData("Height", "240d")]
    [InlineData("HorizontalContentAlignment", "Avalonia.Layout.HorizontalAlignment.Left")]
    public async Task BusinessCodeCannotReplaceSummaryComposition(string property, string value)
    {
        var source = $$"""
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View { public void Configure() { var summary = new AppFileChangeSummary { {{property}} = {{value}} }; } }
            """;
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("Content")]
    [InlineData("Padding")]
    [InlineData("Width")]
    [InlineData("DataTemplates")]
    [InlineData("HorizontalContentAlignment")]
    public void BusinessMarkupCannotReplaceSummaryComposition(string property)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        var project = XDocument.Load(Path.Combine(directory!.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = project.Descendants("Target").Single(item => item.Attribute("Name")?.Value == "ValidateSharedFileChangeSummaries");
        var query = target.Element("XmlPeek")!.Attribute("Query")!.Value;
        foreach (var body in new[] { $"<ui:AppFileChangeSummary {property}='value' />",
            $"<ui:AppFileChangeSummary ui:AppFileChangeSummary.{property}='value' />",
            $"<ui:AppFileChangeSummary><ui:AppFileChangeSummary.{property}>value</ui:AppFileChangeSummary.{property}></ui:AppFileChangeSummary>" })
        {
            var document = XDocument.Parse($"<root xmlns:ui='clr-namespace:MossAgent.App.Controls'>{body}</root>");
            Assert.Single(document.XPathSelectElements(query));
        }
        Assert.Empty(XDocument.Parse("<root xmlns:ui='clr-namespace:MossAgent.App.Controls'><ui:AppFileChangeSummary Summary='{Binding FileChanges}' /></root>")
            .XPathSelectElements(query));
    }
}
