using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedTemplateBoundaryTests
{
    [Theory]
    [InlineData("<ui:AppButton Theme='{StaticResource CustomTheme}' />", "AppButton")]
    [InlineData("<ui:AppButton Template='{Binding CustomTemplate}' />", "AppButton")]
    [InlineData("<ui:AppInput FocusAdorner='{x:Null}' />", "AppInput")]
    [InlineData("<ui:AppButton><Button.Theme>custom</Button.Theme></ui:AppButton>", "Button.Theme")]
    [InlineData("<ui:AppButton><ui:AppButton.Template>custom</ui:AppButton.Template></ui:AppButton>", "AppButton.Template")]
    [InlineData("<ui:AppInput><Control.FocusAdorner>custom</Control.FocusAdorner></ui:AppInput>", "Control.FocusAdorner")]
    [InlineData("<ui:AppButton><Control.FocusAdorner><Template><ui:AppBorder /></Template></Control.FocusAdorner></ui:AppButton>", "Control.FocusAdorner")]
    [InlineData("<StyleInclude Source='custom.axaml' />", "StyleInclude")]
    [InlineData("<ui:AppUserControl.Styles />", "AppUserControl.Styles")]
    [InlineData("<Style Selector='ui|AppButton' />", "Style")]
    [InlineData("<ControlTheme />", "ControlTheme")]
    public void ViewMarkup_CannotReplaceComponentTemplatesOrImportLocalStyles(string markup, string offender)
    {
        var document = Parse(markup);

        Assert.Equal([offender], document.XPathSelectElements(GuardQuery())
            .Select(element => element.Name.LocalName));
    }

    [Fact]
    public void SharedVariantsAndFunctionalTemplates_AreAllowed()
    {
        var document = Parse("""
            <Grid>
              <ui:AppIconButton Classes="navigation-history" Label="Back">
                <Button.Flyout><ui:AppFlyout /></Button.Flyout>
              </ui:AppIconButton>
              <ui:AppItemsControl ItemsSource="{Binding Items}">
                <ItemsControl.ItemTemplate><DataTemplate><ui:AppText Text="{Binding Name}" /></DataTemplate></ItemsControl.ItemTemplate>
                <ItemsControl.ItemsPanel><ItemsPanelTemplate><StackPanel /></ItemsPanelTemplate></ItemsControl.ItemsPanel>
              </ui:AppItemsControl>
              <ui:AppUserControl.Resources><SolidColorBrush x:Key="PreviewBrush" Color="Red" /></ui:AppUserControl.Resources>
            </Grid>
            """);

        Assert.Empty(document.XPathSelectElements(GuardQuery()));
    }

    [Fact]
    public void TemplateGuard_CoversNewMarkupDirectoriesButExcludesSharedStylesAndGeneratedFiles()
    {
        var target = GuardTarget();
        var item = Assert.Single(target.Descendants(), element => element.Name.LocalName == "_TemplateBoundaryMarkup");

        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Equal("$(MSBuildProjectDirectory)/**/*.axaml", item.Attribute("Include")?.Value);
        var exclusions = Assert.IsType<string>(item.Attribute("Exclude")?.Value);
        foreach (var exclusion in new[] { "/App.axaml", "/Styles/**/*.axaml", "/bin/**/*.axaml", "/obj/**/*.axaml" })
            Assert.Contains(exclusion, exclusions);
    }

    [Fact]
    public void BusinessMarkup_DoesNotOverrideSharedTemplates()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var query = GuardQuery();
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var segments = Path.GetRelativePath(appPath, file).Split(Path.DirectorySeparatorChar);
            if (segments[0] is "App.axaml" or "Styles" or "bin" or "obj") continue;
            var offenders = XDocument.Load(file).XPathSelectElements(query).Select(element => element.Name.LocalName);

            Assert.True(!offenders.Any(), $"{file} replaces shared appearance: {string.Join(", ", offenders)}");
        }
    }

    [Theory]
    [InlineData("navigation-history", "HistoryNavigationButtonTheme")]
    [InlineData("window-control", "WindowControlButtonTheme")]
    public void ChromeTemplates_AreSelectedBySharedVariants(string variant, string theme)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var style = Assert.Single(document.Descendants(), element =>
            element.Attribute("Selector")?.Value == $"controls|AppButton.{variant}");

        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "Theme"
            && element.Attribute("Value")?.Value == $"{{StaticResource {theme}}}");
    }

    private static XDocument Parse(string markup) => XDocument.Parse($$"""
        <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                           xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
                           xmlns:ui="clr-namespace:MossAgent.App.Controls">
          {{markup}}
        </ui:AppUserControl>
        """);

    private static XElement GuardTarget()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "MossAgent.App.csproj"));
        return Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedVisualTemplates");
    }

    private static string GuardQuery() => Assert.IsType<string>(
        Assert.Single(GuardTarget().Elements(), element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value);

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
