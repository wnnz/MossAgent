using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerMenuBoundaryTests
{
    [Theory]
    [InlineData("Content")]
    [InlineData("DataTemplates")]
    [InlineData("Padding")]
    [InlineData("Width")]
    [InlineData("Height")]
    [InlineData("MinWidth")]
    [InlineData("MaxWidth")]
    [InlineData("MinHeight")]
    [InlineData("MaxHeight")]
    [InlineData("HorizontalContentAlignment")]
    [InlineData("VerticalContentAlignment")]
    public void MarkupRejectsCompositionAndGeometryOverrides(string property)
    {
        foreach (var body in new[]
        {
            $"<ui:AppModelPickerMenu {property}=\"12\" />",
            $"<ui:AppModelPickerMenu UserControl.{property}=\"12\" />",
            $"<ui:AppModelPickerMenu><ui:AppModelPickerMenu.{property}>12</ui:AppModelPickerMenu.{property}></ui:AppModelPickerMenu>",
            $"<ui:AppModelPickerMenu><UserControl.{property}>12</UserControl.{property}></ui:AppModelPickerMenu>"
        })
            Assert.Single(Markup(body).XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public void MarkupAllowsBusinessBindingsButNotImplicitReplacementContent()
    {
        Assert.Empty(Markup("""
            <ui:AppModelPickerMenu DataContext="{Binding ModelPicker}" IsVisible="{Binding IsOpen}" IsEnabled="{Binding IsReady}" />
            """).XPathSelectElements(GuardQuery()));
        Assert.Single(Markup("<ui:AppModelPickerMenu><ui:AppText Text=\"Replacement\" /></ui:AppModelPickerMenu>")
            .XPathSelectElements(GuardQuery()));
    }

    [Theory]
    [InlineData("var menu = new AppModelPickerMenu { Content = \"replacement\" };")]
    [InlineData("var menu = new AppModelPickerMenu { Width = 400 };")]
    [InlineData("var menu = new AppModelPickerMenu { Padding = new Avalonia.Thickness(12) };")]
    [InlineData("var menu = new AppModelPickerMenu(); menu.ClearValue(UserControl.ContentProperty);")]
    [InlineData("var menu = new AppModelPickerMenu(); menu.SetValue(Control.HeightProperty, 400d);")]
    [InlineData("var menu = new AppModelPickerMenu(); menu.DataTemplates.Clear();")]
    public async Task BusinessCodeRejectsCompositionAndGeometryMutation(string body)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(Source(body),
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Fact]
    public async Task BusinessCodeAllowsDataAndAvailabilityBindings()
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source("""
            var menu = new AppModelPickerMenu { DataContext = new object(), IsVisible = false, IsEnabled = false };
            menu.Bind(Control.DataContextProperty, new Avalonia.Data.Binding("ModelPicker"));
            """), new SharedControlAppearanceAnalyzer()));
    }

    private static string Source(string body) => $$"""
        using Avalonia.Controls;
        using MossAgent.App.Controls;
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
        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"),
            element => element.Attribute("Name")?.Value == "ValidateSharedWorkspacePrompts");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
    }
}
