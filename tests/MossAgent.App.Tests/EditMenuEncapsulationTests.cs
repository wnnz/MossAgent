using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EditMenuEncapsulationTests
{
    [Theory]
    [InlineData("menu.Content = \"override\";", "Content")]
    [InlineData("menu.Flyout = new AppFlyout();", "Flyout")]
    [InlineData("menu.Command = null;", "Command")]
    [InlineData("menu.CommandParameter = new object();", "CommandParameter")]
    [InlineData("menu.Height = 90;", "Height")]
    [InlineData("menu.Padding = new Avalonia.Thickness(24);", "Padding")]
    [InlineData("menu.SetValue(Button.FlyoutProperty, new AppFlyout());", "Flyout")]
    [InlineData("Button button = menu; button.Content = \"override\";", "Content")]
    public async Task BusinessCode_CannotReplaceOwnedEditingMenu(string body, string property)
    {
        var source = $$"""
            using Avalonia.Controls;
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppEditMenu menu) { {{body}} }
            }
            """;

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));
        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Contains(property, diagnostic.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Content=\"override\"")]
    [InlineData("Command=\"{Binding Command}\"")]
    [InlineData("Height=\"90\"")]
    [InlineData("Button.Padding=\"20\"")]
    public void Markup_CannotOverrideOwnedMenuAttributes(string attributes)
    {
        var document = XDocument.Parse($$"""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppEditMenu {{attributes}} />
            </ui:AppUserControl>
            """);

        Assert.Equal("AppEditMenu", Assert.Single(document.XPathSelectElements(GuardQuery())).Name.LocalName);
    }

    [Theory]
    [InlineData("<Button.Flyout><ui:AppFlyout /></Button.Flyout>")]
    [InlineData("<Button.Content>override</Button.Content>")]
    [InlineData("<ui:AppText Text=\"extra\" />")]
    public void Markup_CannotReplaceOwnedMenuStructure(string content)
    {
        var document = XDocument.Parse($$"""
            <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                               xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppEditMenu>{{content}}</ui:AppEditMenu>
            </ui:AppUserControl>
            """);

        Assert.Single(document.XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public async Task SharedMenuAndStructuralLayout_RemainAllowed()
    {
        var source = """
            using MossAgent.App.Controls;
            namespace MossAgent.App.Views;
            public class View
            {
                public void Configure(AppEditMenu menu)
                {
                    menu.IsVisible = true;
                    menu.Classes.Add("menu-trigger");
                    menu.Margin = new Avalonia.Thickness(4);
                }
            }
            """;
        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
        var root = RepositoryRoot();
        var window = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "MainWindow.axaml"));
        Assert.Single(window.Descendants(), element => element.Name.LocalName == "AppEditMenu");
        Assert.Empty(window.XPathSelectElements(GuardQuery()));
        Assert.DoesNotContain(window.Descendants(), element => element.Attribute("Tag")?.Value is "copy" or "cut");
    }

    private static string GuardQuery()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedEditMenus");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.IsType<string>(Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
