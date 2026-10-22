using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TerminalVtPolicyTests
{
    [Fact]
    public async Task CodeGuardProtectsScreenMetricsAndViewportComposition()
    {
        var diagnostics = await VisualPolicyTestCompilation.Analyze("""
            using MossAgent.App.Controls;
            public class Sample
            {
                public void Change(AppTerminalScreen screen, AppTerminalViewport viewport)
                {
                    screen.FontSize = 30;
                    screen.CellHeight = 40;
                    screen.Width = 500;
                    screen.Background = Avalonia.Media.Brushes.Red;
                    viewport.Content = new AppText();
                    viewport.Width = 300;
                    viewport.DataTemplates.Clear();
                    viewport.Frame = null;
                    screen.Frame = null;
                }
            }
            """, new SharedControlAppearanceAnalyzer());
        Assert.Equal(7, diagnostics.Count(diagnostic => diagnostic.Id == "MOSSUI002"));
    }

    [Fact]
    public void MarkupGuardRejectsMetricsAndReplacementWithoutBlockingFrameData()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var project = AppBuildDefinition.Load();
        var query = Assert.Single(project.Descendants(), element => element.Attribute("Name")?.Value == "ValidateSharedTerminalPane")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        var document = XDocument.Parse("""
            <Root xmlns:ui="clr-namespace:MossAgent.App.Controls" xmlns="https://github.com/avaloniaui">
              <ui:AppTerminalScreen CellHeight="40" />
              <ui:AppTerminalScreen><ui:AppTerminalScreen.CellHeight>40</ui:AppTerminalScreen.CellHeight></ui:AppTerminalScreen>
              <ui:AppTerminalScreen Width="300" />
              <ui:AppTerminalViewport Content="replacement" />
              <ui:AppTerminalViewport><Grid /></ui:AppTerminalViewport>
              <ui:AppTerminalViewport><ContentControl.Content><Grid /></ContentControl.Content></ui:AppTerminalViewport>
              <ui:AppTerminalViewport Frame="binding" ResizeCommand="binding" InputCommand="binding" IsVisible="binding" Grid.Row="2" />
              <ui:AppTerminalScreen Frame="binding" />
            </Root>
            """);
        Assert.Equal(6, document.XPathSelectElements(query).Count());
    }
}
