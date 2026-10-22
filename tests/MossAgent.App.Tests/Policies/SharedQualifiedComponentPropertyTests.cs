using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedQualifiedComponentPropertyTests
{
    [Theory]
    [InlineData("<ui:AppButton Button.Width='90' />")]
    [InlineData("<ui:AppIconButton Button.Height='90' />")]
    [InlineData("<ui:AppActionButton Button.MinWidth='90' />")]
    [InlineData("<ui:AppSettingsNavButton Button.MaxHeight='90' />")]
    [InlineData("<ui:AppToggleButton Width='90' />")]
    [InlineData("<ui:AppCheckBox MinHeight='90' />")]
    [InlineData("<ui:AppButton Button.HorizontalContentAlignment='Left' />")]
    [InlineData("<ui:AppIconButton Button.VerticalContentAlignment='Top' />")]
    [InlineData("<ui:AppIcon MinWidth='90' />")]
    [InlineData("<ui:AppIcon MaxHeight='90' />")]
    [InlineData("<ui:AppIcon ui:AppIcon.Size='90' />")]
    [InlineData("<ui:AppIcon><Control.MinWidth>90</Control.MinWidth></ui:AppIcon>")]
    [InlineData("<ui:AppColorSwatch Border.MinHeight='90' />")]
    [InlineData("<ui:AppViewbox Viewbox.MaxWidth='90' />")]
    [InlineData("<ui:AppButton Button.Padding='90' />")]
    [InlineData("<ui:AppCard Border.Padding='90' />")]
    [InlineData("<ui:AppInput TextBox.Padding='90' />")]
    [InlineData("<ui:AppComboBox ComboBox.Padding='90' />")]
    [InlineData("<ui:AppNumericUpDown NumericUpDown.Padding='90' />")]
    [InlineData("<ui:AppListBox ListBox.Padding='90' />")]
    [InlineData("<ui:AppIconLabel Grid.MaxHeight='90' />")]
    public void SizingAndInsets_CannotBypassSharedVariants(string markup)
    {
        Assert.Single(Document(markup).XPathSelectElements(Query("ValidateSharedComponentAppearance")));
    }

    [Theory]
    [InlineData("<ui:AppIconLabel Grid.ColumnSpacing='90' />")]
    [InlineData("<ui:AppIconLabel Grid.RowSpacing='90' />")]
    [InlineData("<ui:AppIconLabel Grid.ColumnDefinitions='*,*' />")]
    [InlineData("<ui:AppIconLabel Grid.RowDefinitions='*,*' />")]
    [InlineData("<ui:AppActionButton Button.Content='override' />")]
    [InlineData("<ui:AppIconButton Button.Content='override' />")]
    [InlineData("<ui:AppSettingsNavButton Button.Content='override' />")]
    public void Composition_CannotBypassOwnedContent(string markup)
    {
        Assert.Single(Document(markup).XPathSelectElements(Query("ValidateSharedComposedControls")));
    }

    [Fact]
    public void FunctionalPropertiesAndStructuralLayout_RemainAllowed()
    {
        var document = Document("""
            <Grid Width="736" Grid.ColumnSpacing="12">
              <ui:AppButton Button.Content="Save" Classes="primary" IsEnabled="False" />
              <ui:AppIconButton Label="Close" IconData="{StaticResource UiIconClose}">
                <Button.Flyout><ui:AppFlyout /></Button.Flyout>
              </ui:AppIconButton>
              <ui:AppInput TextBox.Text="value" Width="240" />
              <ui:AppScrollViewer ScrollViewer.Padding="8" />
              <ui:AppIconLabel Label="Project" Classes="project-summary-title" />
              <ui:AppCard Classes="card-inset-roomy" />
            </Grid>
            """);

        Assert.Empty(document.XPathSelectElements(Query("ValidateSharedComponentAppearance")));
        Assert.Empty(document.XPathSelectElements(Query("ValidateSharedComposedControls")));
    }

    [Fact]
    public void AllBusinessMarkup_UsesSharedSizingAndComposition()
    {
        var appPath = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App");
        var queries = new[] { Query("ValidateSharedComponentAppearance"), Query("ValidateSharedComposedControls") };
        foreach (var file in Directory.EnumerateFiles(appPath, "*.axaml", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(appPath, file);
            if (relative == "App.axaml" || relative.Split(Path.DirectorySeparatorChar)[0]
                is "Styles" or "bin" or "obj") continue;

            var document = XDocument.Load(file);
            foreach (var query in queries)
                Assert.True(!document.XPathSelectElements(query).Any(), $"{relative} overrides shared component appearance.");
        }
    }

    private static XDocument Document(string markup) => XDocument.Parse($$"""
        <ui:AppUserControl xmlns="https://github.com/avaloniaui"
                           xmlns:ui="clr-namespace:MossAgent.App.Controls">
          {{markup}}
        </ui:AppUserControl>
        """);

    private static string Query(string targetName)
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), target => target.Attribute("Name")?.Value == targetName);
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

}
