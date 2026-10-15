using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedComponentPropertyElementTests
{
    [Fact]
    public void BuildGuard_RejectsAppearancePropertiesButAllowsBehaviorAndLayout()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppButton>
                <ui:AppButton.Background>Red</ui:AppButton.Background>
                <ui:AppButton.Width>32</ui:AppButton.Width>
                <ui:AppButton.Flyout><ui:AppFlyout /></ui:AppButton.Flyout>
              </ui:AppButton>
              <ui:AppIcon><ui:AppIcon.Size>20</ui:AppIcon.Size></ui:AppIcon>
              <ui:AppInput><ui:AppInput.FontSize>20</ui:AppInput.FontSize></ui:AppInput>
              <ui:AppCard><ui:AppCard.Padding>8</ui:AppCard.Padding></ui:AppCard>
              <ui:AppBorder><ui:AppBorder.CornerRadius>4</ui:AppBorder.CornerRadius></ui:AppBorder>
              <ui:AppColorSwatch><ui:AppColorSwatch.Width>24</ui:AppColorSwatch.Width></ui:AppColorSwatch>
              <ui:AppViewbox><ui:AppViewbox.Height>80</ui:AppViewbox.Height></ui:AppViewbox>
              <ui:MarkdownSelectableTextBlock><ui:MarkdownSelectableTextBlock.Foreground>Red</ui:MarkdownSelectableTextBlock.Foreground></ui:MarkdownSelectableTextBlock>
              <ui:AppScrollViewer><ui:AppScrollViewer.Padding>8</ui:AppScrollViewer.Padding></ui:AppScrollViewer>
              <ui:AppItemsControl><ui:AppItemsControl.ItemTemplate><DataTemplate /></ui:AppItemsControl.ItemTemplate></ui:AppItemsControl>
            </UserControl>
            """);

        Assert.Equal(["AppButton.Background", "AppButton.Width", "AppIcon.Size",
            "AppInput.FontSize", "AppCard.Padding", "AppBorder.CornerRadius",
            "AppColorSwatch.Width", "AppViewbox.Height",
            "MarkdownSelectableTextBlock.Foreground"],
            document.XPathSelectElements(GuardQuery()).Select(element => element.Name.LocalName));
    }

    [Fact]
    public void Views_DoNotBypassSharedAppearanceThroughPropertyElements()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var directory in new[] { "Views", "Controls" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, directory),
                         "*.axaml", SearchOption.AllDirectories))
            {
                var offenders = XDocument.Load(file).XPathSelectElements(GuardQuery())
                    .Select(element => element.Name.LocalName).ToArray();
                Assert.True(offenders.Length == 0,
                    $"{Path.GetFileName(file)} overrides shared appearance: {string.Join(", ", offenders)}");
            }
        }
    }

    [Fact]
    public void BuildGuard_RejectsInheritedAppearancePropertiesButAllowsBehaviorAndLayout()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls"
                         xmlns:wrapped="using:MossAgent.App.Controls"
                         xmlns:assembled="clr-namespace:MossAgent.App.Controls;assembly=MossAgent.App">
              <ui:AppButton>
                <Button.Background>Red</Button.Background>
                <Button.Width>32</Button.Width>
                <Button.Flyout><ui:AppFlyout /></Button.Flyout>
              </ui:AppButton>
              <ui:AppCard><Border.BoxShadow>0 0 4 Red</Border.BoxShadow></ui:AppCard>
              <ui:AppInput><TextBox.FontSize>20</TextBox.FontSize></ui:AppInput>
              <wrapped:AppButton><Button.Foreground>Red</Button.Foreground></wrapped:AppButton>
              <assembled:AppCard><Border.CornerRadius>4</Border.CornerRadius></assembled:AppCard>
              <ui:AppColorSwatch><Border.Width>24</Border.Width></ui:AppColorSwatch>
              <ui:AppViewbox><Viewbox.Height>80</Viewbox.Height></ui:AppViewbox>
              <ui:AppScrollViewer><ScrollViewer.Padding>8</ScrollViewer.Padding></ui:AppScrollViewer>
              <ui:MarkdownSelectableTextBlock>
                <SelectableTextBlock.Foreground>Red</SelectableTextBlock.Foreground>
              </ui:MarkdownSelectableTextBlock>
            </UserControl>
            """);

        Assert.Equal(["Button.Background", "Button.Width", "Border.BoxShadow",
            "TextBox.FontSize", "Button.Foreground", "Border.CornerRadius",
            "Border.Width", "Viewbox.Height",
            "SelectableTextBlock.Foreground"],
            document.XPathSelectElements(InheritedGuardQuery()).Select(element => element.Name.LocalName));
    }

    [Fact]
    public void Views_DoNotBypassSharedAppearanceThroughInheritedProperties()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        foreach (var directory in new[] { "Views", "Controls" })
        {
            foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, directory),
                         "*.axaml", SearchOption.AllDirectories))
            {
                Assert.Empty(XDocument.Load(file).XPathSelectElements(InheritedGuardQuery()));
            }
        }
    }

    private static string InheritedGuardQuery()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateInheritedAppearancePropertyElements");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("Styles/**/*.axaml", target.ToString());
        var peek = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek");
        return Assert.IsType<string>(peek.Attribute("Query")?.Value);
    }

    private static string GuardQuery()
    {
        var project = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedPropertyElements");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var peek = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek");
        return Assert.IsType<string>(peek.Attribute("Query")?.Value);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
