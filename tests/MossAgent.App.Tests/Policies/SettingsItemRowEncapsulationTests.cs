using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SettingsItemRowEncapsulationTests
{
    [Theory]
    [InlineData("Content = \"override\"")]
    [InlineData("Width = 80")]
    [InlineData("Height = 80")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    [InlineData("VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Top")]
    [InlineData("Background = Avalonia.Media.Brushes.Red")]
    public async Task BusinessCode_CannotOverrideSharedComposition(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppSettingsItemRow { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppSettingsItemRow Width='80' />")]
    [InlineData("<ui:AppSettingsItemRow Control.MinHeight='80' />")]
    [InlineData("<ui:AppSettingsItemRow Padding='12' />")]
    [InlineData("<ui:AppSettingsItemRow Content='override' />")]
    [InlineData("<ui:AppSettingsItemRow><Control.Height>80</Control.Height></ui:AppSettingsItemRow>")]
    [InlineData("<ui:AppSettingsItemRow><ContentControl.Content>override</ContentControl.Content></ui:AppSettingsItemRow>")]
    [InlineData("<ui:AppSettingsItemRow><ui:AppText Text='override' /></ui:AppSettingsItemRow>")]
    public void BusinessMarkup_CannotOverrideSharedComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void DataCommandsAndSubtitleBindings_RemainAllowed()
    {
        var document = Document("""
            <ui:AppSettingsItemRow Title="{Binding Name}" Metadata="{Binding Protocol}"
                  Classes="endpoint" ActionTarget="{Binding}" BadgeText="默认" IsBadgeVisible="{Binding IsDefault}"
                  PrimaryLabel="测试" PrimaryCommand="{Binding Test}" SecondaryLabel="编辑" SecondaryCommand="{Binding Edit}">
              <ui:AppSettingsItemRow.Subtitle>
                <MultiBinding StringFormat="{}{0}:{1}"><Binding Path="Host" /><Binding Path="Port" /></MultiBinding>
              </ui:AppSettingsItemRow.Subtitle>
            </ui:AppSettingsItemRow>
            """);
        Assert.Empty(document.XPathSelectElements(Query()));
    }

    [Fact]
    public async Task BusinessCode_AllowsSemanticDataAndSharedVariants()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render()
                {
                    var row = new AppSettingsItemRow { Title = "name", Subtitle = "endpoint", ActionTarget = new object() };
                    row.Classes.Add("compact");
                }
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedVisualControlAnalyzer()));
    }

    [Theory]
    [InlineData("ProxySettingsPane")]
    [InlineData("McpSettingsPane")]
    [InlineData("AgentSettingsPane")]
    [InlineData("SubagentSettingsPane")]
    public void SettingsLists_UseTheSameComposedRow(string pane)
    {
        var document = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", pane + ".axaml"));
        Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppSettingsItemRow");
        Assert.Empty(document.XPathSelectElements(Query()));
        var template = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ListBox.ItemTemplate");
        Assert.DoesNotContain(template.Descendants(), element => element.Name.LocalName is "AppButton" or "Grid" or "StackPanel");
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedProviderSettingsRows");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
