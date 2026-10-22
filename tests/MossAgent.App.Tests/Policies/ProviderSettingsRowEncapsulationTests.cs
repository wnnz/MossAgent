using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProviderSettingsRowEncapsulationTests
{
    [Theory]
    [InlineData("AppProviderSettingsHeader", "Content = \"override\"")]
    [InlineData("AppProviderSettingsHeader", "Width = 48")]
    [InlineData("AppProviderSettingsHeader", "Padding = new Avalonia.Thickness(12)")]
    [InlineData("AppModelSettingsRow", "Content = \"override\"")]
    [InlineData("AppModelSettingsRow", "Height = 48")]
    [InlineData("AppModelSettingsRow", "Padding = new Avalonia.Thickness(12)")]
    public async Task BusinessCode_CannotReplaceRowComposition(string component, string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new " + component + " { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppProviderSettingsHeader Width='32' />")]
    [InlineData("<ui:AppProviderSettingsHeader Control.Height='32' />")]
    [InlineData("<ui:AppProviderSettingsHeader><ContentControl.Content>override</ContentControl.Content></ui:AppProviderSettingsHeader>")]
    [InlineData("<ui:AppModelSettingsRow Padding='12' />")]
    [InlineData("<ui:AppModelSettingsRow><ui:AppText Text='override' /></ui:AppModelSettingsRow>")]
    [InlineData("<ui:AppModelSettingsRow><Control.Width>32</Control.Width></ui:AppModelSettingsRow>")]
    public void BusinessMarkup_CannotReplaceRowComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Theory]
    [InlineData("<ui:AppProviderSettingsHeader Provider='{Binding Provider}' ModelCount='{Binding ModelCount}' ToggleCommand='{Binding Toggle}' ToggleCommandParameter='{Binding}' IsExpanded='{Binding Expanded}' />")]
    [InlineData("<ui:AppModelSettingsRow Model='{Binding}' EditCommand='{Binding Edit}' DeleteCommand='{Binding Delete}' IsVisible='{Binding Visible}' />")]
    public void BusinessMarkup_AllowsFunctionalBindings(string markup) =>
        Assert.Empty(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public void SettingsPage_UsesBothComposedRowsWithoutReplacingTheirContents()
    {
        var document = XDocument.Load(Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views", "ProviderAndModelSettingsPane.axaml"));
        var header = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppProviderSettingsHeader");
        var row = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppModelSettingsRow");
        Assert.Equal("{Binding Provider}", header.Attribute("Provider")?.Value);
        Assert.Equal("{Binding}", header.Attribute("ToggleCommandParameter")?.Value);
        Assert.Equal("{Binding}", row.Attribute("Model")?.Value);
        Assert.Contains("EditProviderModelCommand", row.Attribute("EditCommand")!.Value, StringComparison.Ordinal);
        Assert.Contains("DeleteProviderModelCommand", row.Attribute("DeleteCommand")!.Value, StringComparison.Ordinal);
        Assert.Empty(document.XPathSelectElements(Query()));
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
