using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class NumericFieldBoundaryTests
{
    [Theory]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("MinWidth")]
    [InlineData("MaxWidth")]
    [InlineData("MinHeight")]
    [InlineData("MaxHeight")]
    [InlineData("Padding")]
    [InlineData("HorizontalContentAlignment")]
    [InlineData("VerticalContentAlignment")]
    public void Markup_CannotOverrideRepeatButtonGeometry(string property)
    {
        foreach (var body in new[]
                 {
                     $"<ui:AppRepeatButton {property}=\"12\" />",
                     $"<ui:AppRepeatButton ui:AppRepeatButton.{property}=\"12\" />",
                     $"<ui:AppRepeatButton><ui:AppRepeatButton.{property}>12</ui:AppRepeatButton.{property}></ui:AppRepeatButton>",
                     $"<ui:AppRepeatButton><Button.{property}>12</Button.{property}></ui:AppRepeatButton>"
                 })
        {
            Assert.Single(Markup(body).XPathSelectElements(GuardQuery()));
        }
    }

    [Fact]
    public void Markup_PreservesSharedImplementationAndFunctionalProperties()
    {
        Assert.Empty(Markup("""
            <ui:AppRepeatButton Content="Repeat" Command="{Binding Repeat}" IsEnabled="{Binding CanRepeat}">
              <ui:AppRepeatButton.Flyout><ui:AppFlyout /></ui:AppRepeatButton.Flyout>
            </ui:AppRepeatButton>
            """).XPathSelectElements(GuardQuery()));
        var theme = XDocument.Load(Path.Combine(AppPath(), "Styles", "NumericFieldVariants.axaml"));
        Assert.Equal(2, theme.Descendants().Count(element => element.Name.LocalName == "AppRepeatButton"));
        Assert.DoesNotContain(theme.Descendants(), element => element.Name.LocalName is "RepeatButton" or "PathIcon");
    }

    [Theory]
    [InlineData("_ = new MossAgent.App.Controls.AppRepeatButton { Width = 40 };")]
    [InlineData("_ = new MossAgent.App.Controls.AppRepeatButton { Padding = new Avalonia.Thickness(8) };")]
    [InlineData("var button = new MossAgent.App.Controls.AppRepeatButton(); button.SetValue(Avalonia.Controls.Control.HeightProperty, 40d);")]
    public async Task BusinessCode_CannotOverrideRepeatButtonAppearance(string body)
    {
        var source = $$"""
            namespace MossAgent.App.Views;
            public static class Probe { public static void Create() { {{body}} } }
            """;
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    private static XDocument Markup(string body) => XDocument.Parse($$"""
        <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                          xmlns:ui="clr-namespace:MossAgent.App.Controls">
          <StackPanel>{{body}}</StackPanel>
        </ui:AppUserControl>
        """);

    private static string GuardQuery()
    {
        var project = AppBuildDefinition.Load();
        return Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedRepeatButtons")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
    }

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
