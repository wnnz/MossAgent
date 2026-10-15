using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalPanePolicyTests
{
    [Fact]
    public async Task CodeGuardRejectsTerminalCompositionAndGeometryReplacement()
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze("""
            using MossAgent.App.Controls;
            public class Sample
            {
                public void Change(AppTerminalPane pane)
                {
                    pane.Content = new AppText();
                    pane.Padding = new Avalonia.Thickness(3);
                    pane.Width = 400;
                    pane.DataTemplates.Clear();
                    pane.DataContext = new object();
                    pane.IsVisible = true;
                }
            }
            """, new SharedControlAppearanceAnalyzer());
        Assert.Equal(4, diagnostics.Count(diagnostic => diagnostic.Id == "MOSSUI002"));
    }

    [Fact]
    public void MarkupGuardRejectsExplicitAndImplicitContentButAllowsTaskBindings()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var query = Assert.Single(project.Descendants(), element => element.Attribute("Name")?.Value == "ValidateSharedTerminalPane")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        var document = XDocument.Parse("""
            <Root xmlns:ui="clr-namespace:MossAgent.App.Controls" xmlns="https://github.com/avaloniaui">
              <ui:AppTerminalPane Content="replacement" />
              <ui:AppTerminalPane Width="300" />
              <ui:AppTerminalPane><Grid /></ui:AppTerminalPane>
              <ui:AppTerminalPane><ContentControl.Content><Grid /></ContentControl.Content></ui:AppTerminalPane>
              <ui:AppTerminalPane><ui:AppTerminalPane.Padding>3</ui:AppTerminalPane.Padding></ui:AppTerminalPane>
              <ui:AppTerminalPane DataContext="binding" IsVisible="binding" Grid.Row="2" />
            </Root>
            """);
        Assert.Equal(5, document.XPathSelectElements(query).Count());
    }
}
