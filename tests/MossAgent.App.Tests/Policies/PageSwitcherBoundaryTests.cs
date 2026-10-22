using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class PageSwitcherBoundaryTests
{
    [Theory]
    [InlineData("Content=\"replacement\"", "")]
    [InlineData("", "<ui:AppButton />")]
    [InlineData("", "<UserControl.Content><Grid /></UserControl.Content>")]
    public void MarkupCannotReplaceOwnedPresenters(string attributes, string content)
    {
        var markup = XDocument.Parse($$"""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppPageSwitcher {{attributes}}>{{content}}</ui:AppPageSwitcher>
            </ui:AppUserControl>
            """);
        Assert.Equal("AppPageSwitcher", Assert.Single(markup.XPathSelectElements(GuardQuery())).Name.LocalName);
    }

    [Fact]
    public void SemanticPagePropertiesRemainAllowed()
    {
        var markup = XDocument.Parse("""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppPageSwitcher IsSecondaryActive="{Binding IsSettingsOpen}">
                <ui:AppPageSwitcher.PrimaryPage><Grid /></ui:AppPageSwitcher.PrimaryPage>
                <ui:AppPageSwitcher.SecondaryPage><Grid /></ui:AppPageSwitcher.SecondaryPage>
              </ui:AppPageSwitcher>
            </ui:AppUserControl>
            """);
        Assert.Empty(markup.XPathSelectElements(GuardQuery()));
    }

    [Theory]
    [InlineData("switcher.Content = new Grid();")]
    [InlineData("switcher.SetValue(UserControl.ContentProperty, new Grid());")]
    public async Task BusinessCodeCannotReplaceOwnedPresenters(string body)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppPageSwitcher switcher) { {{body}} }
            }
            """;
        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains("Content", diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task BusinessCodeCanSelectAndPopulatePages()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppPageSwitcher switcher)
                {
                    switcher.PrimaryPage = new AppInput();
                    switcher.SecondaryPage = new AppInput();
                    switcher.IsSecondaryActive = true;
                }
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string GuardQuery()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedPageSwitchers");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.IsType<string>(Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value);
    }

}
