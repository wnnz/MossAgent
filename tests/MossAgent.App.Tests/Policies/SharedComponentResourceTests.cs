using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedComponentResourceTests
{
    [Theory]
    [InlineData("button.Resources[\"ButtonBackground\"] = Avalonia.Media.Brushes.Red;")]
    [InlineData("button.Resources.Add(\"ButtonBackground\", Avalonia.Media.Brushes.Red);")]
    [InlineData("button.Resources.Clear();")]
    [InlineData("_ = System.Collections.Generic.CollectionExtensions.TryAdd(button.Resources, \"ButtonBackground\", Avalonia.Media.Brushes.Red);")]
    [InlineData("button.Resources.Remove(\"ButtonBackground\");")]
    [InlineData("button.Resources = new Avalonia.Controls.ResourceDictionary();")]
    [InlineData("_ = new MossAgent.App.Controls.AppButton { Resources = { [\"ButtonBackground\"] = Avalonia.Media.Brushes.Red } };")]
    [InlineData("_ = new MossAgent.App.Controls.AppButton { Resources = { { \"ButtonBackground\", Avalonia.Media.Brushes.Red } } };")]
    [InlineData("var resources = button.Resources; resources[\"ControlCornerRadius\"] = new Avalonia.CornerRadius(0);")]
    [InlineData("var resources = (System.Collections.Generic.IDictionary<object, object?>)button.Resources; resources.Clear();")]
    [InlineData("button.Resources.MergedDictionaries.Add(new Avalonia.Controls.ResourceDictionary());")]
    [InlineData("var merged = button.Resources.MergedDictionaries; merged.Clear();")]
    [InlineData("((Avalonia.Controls.ResourceDictionary)button.Resources.MergedDictionaries[0]).Add(\"ButtonBackground\", Avalonia.Media.Brushes.Red);")]
    [InlineData("button.Resources.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Dark] = new Avalonia.Controls.ResourceDictionary();")]
    [InlineData("var themes = button.Resources.ThemeDictionaries; themes.Clear();")]
    [InlineData("button.Resources.ThemeDictionaries.Add(Avalonia.Styling.ThemeVariant.Dark, new Avalonia.Controls.ResourceDictionary());")]
    [InlineData("var dictionary = (Avalonia.Controls.ResourceDictionary)button.Resources.ThemeDictionaries[Avalonia.Styling.ThemeVariant.Dark]; dictionary[\"ButtonBackground\"] = Avalonia.Media.Brushes.Red;")]
    public async Task BusinessCode_CannotReplaceSharedAppearanceThroughLocalResources(string statement)
    {
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(Source(statement),
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Resources", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("_ = button.Resources.Count;")]
    [InlineData("_ = button.Resources[\"ButtonBackground\"];")]
    [InlineData("_ = button.Resources.ContainsKey(\"ButtonBackground\");")]
    [InlineData("_ = button.Resources.TryGetResource(\"ButtonBackground\", null, out var value);")]
    [InlineData("_ = button.Resources.MergedDictionaries.Count;")]
    [InlineData("_ = button.Resources.ThemeDictionaries.ContainsKey(Avalonia.Styling.ThemeVariant.Dark);")]
    [InlineData("Avalonia.Application.Current!.Resources[\"UiFontFamily\"] = new Avalonia.Media.FontFamily(\"Inter\");")]
    [InlineData("var resources = new Avalonia.Controls.ResourceDictionary(); resources[\"Value\"] = 1;")]
    public async Task ReadOnlyResourcesAndApplicationAppearance_RemainAllowed(string statement)
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source(statement),
            new SharedControlAppearanceAnalyzer()));
    }

    [Fact]
    public async Task SharedComponentImplementation_CanOwnItsResources()
    {
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(Source(
            "button.Resources[\"ControlCornerRadius\"] = new Avalonia.CornerRadius(8); "
            + "button.Resources.MergedDictionaries.Add(new Avalonia.Controls.ResourceDictionary());",
            "MossAgent.App.Controls"), new SharedControlAppearanceAnalyzer()));
    }

    [Theory]
    [InlineData("<ui:AppButton.Resources><SolidColorBrush x:Key='ButtonBackground'>Red</SolidColorBrush></ui:AppButton.Resources>")]
    [InlineData("<Control.Resources><FontFamily x:Key='UiFontFamily'>Arial</FontFamily></Control.Resources>")]
    [InlineData("<ui:AppButton.Resources><BoxShadows x:Key='SharedSurfaceShadow'>0 8 20 0 Black</BoxShadows></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Resources><StreamGeometry x:Key='UiIconClose'>M0,0 L1,1</StreamGeometry></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Resources><ResourceInclude Source='page-tokens.axaml' /></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries><ResourceInclude Source='page-tokens.axaml' /></ResourceDictionary.MergedDictionaries></ResourceDictionary></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Resources><ResourceDictionary><ResourceDictionary.ThemeDictionaries><ResourceDictionary x:Key='Dark'><SolidColorBrush x:Key='ButtonBackground'>Red</SolidColorBrush></ResourceDictionary></ResourceDictionary.ThemeDictionaries></ResourceDictionary></ui:AppButton.Resources>")]
    [InlineData("<StyleInclude Source='page-style.axaml' />")]
    public void BusinessMarkup_CannotShadowSharedVisualResources(string content)
    {
        Assert.Single(Markup(content).XPathSelectElements(ResourceQuery()));
    }

    [Theory]
    [InlineData("Resources='{Binding LocalResources}'")]
    [InlineData("Control.Resources='{Binding LocalResources}'")]
    public void BusinessMarkup_CannotReplaceResourceDictionary(string attribute)
    {
        Assert.Single(Markup(string.Empty, attribute).XPathSelectElements(ResourceQuery()));
    }

    [Theory]
    [InlineData("<ui:AppButton.Resources><ui:McpServerSummaryConverter x:Key='Summary' /></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Resources><DataTemplate x:Key='Row'><ui:AppText Text='{Binding Title}' /></DataTemplate></ui:AppButton.Resources>")]
    [InlineData("<ui:AppButton.Flyout><ui:AppFlyout><ui:AppCard><ui:AppText Text='Menu' /></ui:AppCard></ui:AppFlyout></ui:AppButton.Flyout>")]
    public void FunctionalResourcesAndBindings_RemainAllowed(string content)
    {
        Assert.Empty(Markup(content).XPathSelectElements(ResourceQuery()));
    }

    [Fact]
    public void AllBusinessMarkup_UsesSharedVisualResources()
    {
        var app = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        var query = ResourceQuery();
        foreach (var file in Directory.EnumerateFiles(app, "*.axaml", SearchOption.AllDirectories)
            .Where(file => !new[] { "bin", "obj", "Styles" }.Any(directory =>
                Path.GetRelativePath(app, file).Split(Path.DirectorySeparatorChar).Contains(directory))
                && Path.GetFileName(file) != "App.axaml"))
        {
            Assert.True(!XDocument.Load(file).XPathSelectElements(query).Any(),
                $"{file} shadows shared visual resources");
        }
    }

    private static string Source(string statement, string scope = "MossAgent.App.Views") => $$"""
        namespace {{scope}};
        public class Host
        {
            public void Configure(MossAgent.App.Controls.AppButton button) { {{statement}} }
        }
        """;

    private static XDocument Markup(string content, string attribute = "") => XDocument.Parse(
        "<ui:AppButton xmlns='https://github.com/avaloniaui' "
        + "xmlns:ui='clr-namespace:MossAgent.App.Controls' "
        + "xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' "
        + attribute + ">" + content + "</ui:AppButton>");

    private static string ResourceQuery()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedComponentResources");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
