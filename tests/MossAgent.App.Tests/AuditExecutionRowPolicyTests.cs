using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuditExecutionRowPolicyTests
{
    [Theory]
    [InlineData("row.Content = new AppText();")]
    [InlineData("row.Width = 500;")]
    [InlineData("row.Padding = new Avalonia.Thickness(20);")]
    public async Task BusinessCodeCannotReplaceTheSharedRow(string mutation)
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze($$"""
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class Sample
            {
                public void Change(AppAuditExecutionRow row) { {{mutation}} }
            }
            """, new SharedControlAppearanceAnalyzer());
        Assert.Equal("MOSSUI002", Assert.Single(diagnostics).Id);
    }

    [Fact]
    public void MarkupGuardRejectsCompositionButAcceptsSemanticData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var query = Assert.Single(project.Descendants(), element => element.Attribute("Name")?.Value == "ValidateSharedInspectorComponents")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        var markup = XDocument.Parse("""
            <Root xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppAuditExecutionRow Content="replacement" /><ui:AppAuditExecutionRow Width="40" />
              <ui:AppAuditExecutionRow><ui:AppText /></ui:AppAuditExecutionRow>
              <ui:AppAuditExecutionRow ToolName="file.read" OutcomeKind="success" Detail="Complete" />
            </Root>
            """);
        Assert.Equal(3, markup.XPathSelectElements(query).Count());
    }
}
