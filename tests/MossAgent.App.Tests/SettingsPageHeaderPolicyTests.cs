using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SettingsPageHeaderPolicyTests
{
    [Theory]
    [InlineData("Content = new AppText()")]
    [InlineData("Width = 32")]
    [InlineData("Padding = new Avalonia.Thickness(12)")]
    [InlineData("FontSize = 24")]
    public async Task BusinessCode_CannotOverrideSharedComposition(string property)
    {
        var source = "using MossAgent.App.Controls; namespace MossAgent.App.Views; public class View { "
            + "public void Render() { _ = new AppSettingsPageHeader { " + property + " }; } }";
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppSettingsPageHeader Width='32' />")]
    [InlineData("<ui:AppSettingsPageHeader><Control.Padding>12</Control.Padding></ui:AppSettingsPageHeader>")]
    [InlineData("<ui:AppSettingsPageHeader><ContentControl.Content>override</ContentControl.Content></ui:AppSettingsPageHeader>")]
    [InlineData("<ui:AppSettingsPageHeader><StackPanel /></ui:AppSettingsPageHeader>")]
    public void BusinessMarkup_CannotOverrideSharedComposition(string markup) =>
        Assert.Single(Document(markup).XPathSelectElements(Query()));

    [Fact]
    public async Task BusinessCode_AllowsDataAndDedicatedActionSlot()
    {
        const string source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Render() => _ = new AppSettingsPageHeader
                {
                    Title = "settings", Description = "description", Status = "status",
                    IsStatusError = true, Actions = new AppActionButton { Label = "create" }
                };
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        Assert.Empty(Document("""
            <ui:AppSettingsPageHeader Title='{Binding Title}' Status='{Binding Status}'>
              <ui:AppSettingsPageHeader.Actions><StackPanel><ui:AppActionButton Command='{Binding CreateCommand}' /></StackPanel></ui:AppSettingsPageHeader.Actions>
            </ui:AppSettingsPageHeader>
            """).XPathSelectElements(Query()));
    }

    [Theory]
    [InlineData("ProviderAndModelSettingsPane")]
    [InlineData("ProxySettingsPane")]
    [InlineData("McpSettingsPane")]
    [InlineData("AgentSettingsPane")]
    [InlineData("SubagentSettingsPane")]
    public void CatalogPages_UseSharedHeaderAndActionSpacing(string page)
    {
        var document = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "Views", page + ".axaml"));
        var header = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppSettingsPageHeader");
        var actions = Assert.Single(header.Descendants(), element => element.Name.LocalName == "StackPanel");
        Assert.Equal("settings-header-actions", actions.Attribute("Classes")?.Value);
        Assert.Null(actions.Attribute("Spacing"));
        Assert.DoesNotContain(document.Descendants(), element => element.Attribute("Classes")?.Value == "settings-title");
        Assert.Empty(document.XPathSelectElements(Query()));
    }

    private static XDocument Document(string markup) => XDocument.Parse(
        "<Grid xmlns='https://github.com/avaloniaui' xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

    private static string Query()
    {
        var project = XDocument.Load(Path.Combine(Root(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element => element.Attribute("Name")?.Value == "ValidateSharedProviderSettingsRows");
        return Assert.Single(target.Elements("XmlPeek")).Attribute("Query")!.Value;
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
