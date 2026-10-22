using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Xml.XPath;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class VisualComponentUsageTests
{
    private static readonly HashSet<string> SharedVisualStyleProperties =
    [
        "Background", "Foreground", "BorderBrush", "BorderThickness", "CornerRadius",
        "Padding", "FontSize", "FontWeight", "FontFamily", "LineHeight", "BoxShadow", "StrokeThickness",
        "HorizontalContentAlignment", "VerticalContentAlignment", "Template",
        "FocusAdorner", "Cursor", "Width", "Height"
    ];

    private static readonly HashSet<string> InlineButtonAppearanceProperties =
    [
        "Background", "Foreground", "BorderBrush", "BorderThickness", "CornerRadius",
        "Padding", "FontSize", "FontWeight", "BoxShadow", "Cursor", "Width", "Height",
        "MinWidth", "MaxWidth", "MinHeight", "MaxHeight",
        "HorizontalContentAlignment", "VerticalContentAlignment"
    ];

    private static readonly HashSet<string> InlineSurfaceAppearanceProperties =
    [
        "Background", "BorderBrush", "BorderThickness", "CornerRadius", "BoxShadow"
    ];

    private static readonly HashSet<string> InlineFieldAppearanceProperties =
    [
        "Background", "Foreground", "BorderBrush", "BorderThickness", "CornerRadius",
        "BoxShadow", "Padding", "FontSize", "FontWeight"
    ];

    private static readonly HashSet<string> InlineTextAppearanceProperties =
    [
        "FontSize", "FontWeight", "FontFamily", "LineHeight", "Foreground"
    ];

    private static readonly HashSet<string> SharedFieldElements =
    [
        "AppInput", "AppComboBox", "AppNumericUpDown", "AppCheckBox",
        "AppToggleButton", "AppListBox"
    ];

    [Fact]
    public void Axaml_UsesSharedCardAndFlyoutStyles()
    {
        foreach (var file in VisualFiles())
        {
            var document = XDocument.Load(file);
            var inlineShadows = document.Descendants()
                .Where(element => element.Attribute("BoxShadow") is not null
                    || (element.Name.LocalName == "Setter"
                        && element.Attribute("Property")?.Value == "BoxShadow"))
                .Select(element => element.Name.LocalName)
                .ToArray();
            Assert.True(inlineShadows.Length == 0,
                $"{Path.GetFileName(file)} defines local shadows on: {string.Join(", ", inlineShadows)}");

            var unsharedCards = document.Descendants()
                .Where(element => element.Name.LocalName == "AppBorder"
                    && element.Attribute("Classes")?.Value.Split(' ').Contains("card") == true)
                .ToArray();
            Assert.True(unsharedCards.Length == 0,
                $"{Path.GetFileName(file)} applies card styling without AppCard");

            var unsharedFlyoutCards = document.Descendants()
                .Where(element => element.Name.LocalName == "AppFlyout"
                    && element.Elements().Any(child => child.Name.LocalName == "AppBorder"))
                .ToArray();
            Assert.True(unsharedFlyoutCards.Length == 0,
                $"{Path.GetFileName(file)} uses AppBorder instead of AppCard for a flyout surface");

            var flyoutStyleOverrides = document.Descendants()
                .Where(element => element.Name.LocalName == "AppFlyout"
                    && element.Attribute("FlyoutPresenterClasses") is not null)
                .ToArray();
            Assert.True(flyoutStyleOverrides.Length == 0,
                $"{Path.GetFileName(file)} overrides the shared flyout presenter style");
        }
    }

    [Fact]
    public void Axaml_DoesNotDefineLocalVisualStyles()
    {
        foreach (var file in VisualFiles())
        {
            var localSetters = FindLocalVisualSetters(XDocument.Load(file));
            Assert.True(localSetters.Length == 0,
                $"{Path.GetFileName(file)} defines component visual styles locally: "
                + string.Join(", ", localSetters));
        }
    }

    [Fact]
    public void LocalVisualStylePolicy_RejectsPageSpecificButtonAppearance()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui">
              <UserControl.Styles>
                <Style Selector="Button"><Setter Property="Background" Value="Red" /></Style>
                <Style Selector="Button"><Setter Property="Margin" Value="4" /></Style>
              </UserControl.Styles>
            </UserControl>
            """);

        Assert.Equal(["Background"], FindLocalVisualSetters(document));
    }

    [Fact]
    public void Axaml_ButtonsUseSharedAppearanceVariants()
    {
        foreach (var file in VisualFiles())
        {
            var overrides = FindInlineButtonAppearance(XDocument.Load(file));
            Assert.True(overrides.Length == 0,
                $"{Path.GetFileName(file)} overrides shared button appearance: "
                + string.Join(", ", overrides));
        }
    }

    [Fact]
    public void Axaml_CardsAndFieldsUseSharedAppearanceVariants()
    {
        foreach (var file in VisualFiles())
        {
            var overrides = FindInlineSurfaceAndFieldAppearance(XDocument.Load(file));
            Assert.True(overrides.Length == 0,
                $"{Path.GetFileName(file)} overrides shared component appearance: "
                + string.Join(", ", overrides));
        }
    }

    [Fact]
    public void Axaml_TextUsesSharedTypographyVariants()
    {
        var styleFile = Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App",
            "Styles", "TypographyVariants.axaml");
        var selectors = XDocument.Load(styleFile).Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var file in VisualFiles())
        {
            var document = XDocument.Load(file);
            Assert.Empty(FindInlineTextAppearance(document));
            foreach (var element in document.Descendants().Where(element =>
                element.Name.LocalName is "AppText" or "AppSelectableText"
                    or "MarkdownSelectableTextBlock" or "SyntaxHighlightedSelectableTextBlock"))
            {
                foreach (var variant in (element.Attribute("Classes")?.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(value => value.StartsWith("type-", StringComparison.Ordinal)
                        || value.StartsWith("weight-", StringComparison.Ordinal)
                        || value.StartsWith("line-", StringComparison.Ordinal)))
                {
                    Assert.Contains($"controls|{element.Name.LocalName}.{variant}", selectors);
                }
            }
        }
    }

    [Fact]
    public void TypographyPolicy_RejectsInlineFontOverrides()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppText FontSize="18" />
              <ui:AppSelectableText FontWeight="SemiBold" />
              <ui:AppText Foreground="Red" />
              <ui:AppSelectableText LineHeight="18" FontFamily="Consolas" />
              <ui:MarkdownSelectableTextBlock FontSize="12" />
              <ui:SyntaxHighlightedSelectableTextBlock LineHeight="18" />
            </UserControl>
            """);

        Assert.Equal(["AppText.FontSize", "AppSelectableText.FontWeight", "AppText.Foreground",
            "AppSelectableText.LineHeight", "AppSelectableText.FontFamily",
            "MarkdownSelectableTextBlock.FontSize", "SyntaxHighlightedSelectableTextBlock.LineHeight"],
            FindInlineTextAppearance(document));
    }

    [Fact]
    public void Axaml_DoesNotDeclareLiteralColors()
    {
        foreach (var file in VisualFiles())
        {
            var colors = FindLiteralColors(XDocument.Load(file));
            Assert.True(colors.Length == 0,
                $"{Path.GetFileName(file)} defines colors outside shared themes: "
                + string.Join(", ", colors));
        }
    }

    [Fact]
    public void ColorPolicy_RejectsLiteralColorsInViews()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppBorder Background="#FFFFFF" />
              <ui:AppText Foreground="#fff" />
            </UserControl>
            """);

        Assert.Equal(["AppBorder.Background", "AppText.Foreground"], FindLiteralColors(document));
    }

    [Fact]
    public void SharedAppearancePolicy_RejectsPageSpecificOverrides()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppCard Background="Red" CornerRadius="2" Padding="12" />
              <ui:AppInput FontSize="20" BorderBrush="Red" MinHeight="60" />
              <ui:AppComboBox Padding="2" Width="100" />
              <ui:AppScrollViewer Background="Red" Padding="8" />
              <ui:AppColorSwatch BorderBrush="Red" Width="24" />
              <ui:AppViewbox Height="80" />
            </UserControl>
            """);

        Assert.Equal(["AppCard.Background", "AppCard.CornerRadius", "AppCard.Padding", "AppInput.FontSize",
            "AppInput.BorderBrush", "AppComboBox.Padding", "AppScrollViewer.Background",
            "AppColorSwatch.BorderBrush", "AppColorSwatch.Width", "AppViewbox.Height"],
            FindInlineSurfaceAndFieldAppearance(document));
    }

    [Fact]
    public void ButtonAppearancePolicy_RejectsPageSpecificDimensionsAndColors()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppButton Background="Red" Width="30" Padding="8,4"
                            HorizontalContentAlignment="Left" MinHeight="31" />
              <ui:AppSettingsNavButton Padding="3" />
            </UserControl>
            """);

        Assert.Equal(["Background", "Width", "Padding", "HorizontalContentAlignment", "MinHeight", "Padding"],
            FindInlineButtonAppearance(document));
    }

    [Fact]
    public void BuildGuard_RejectsInlineAppearanceButAllowsLayout()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element =>
            element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedComponentAppearance");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var peek = Assert.Single(target.Elements(), element => element.Name.LocalName == "XmlPeek");
        var query = Assert.IsType<string>(peek.Attribute("Query")?.Value);
        Assert.Contains("$(MSBuildProjectDirectory)/**/*.axaml", target.ToString());
        Assert.Contains("Styles/**/*.axaml", target.ToString());

        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls"
                         xmlns:views="clr-namespace:MossAgent.App.Views">
              <Grid Margin="8">
                <ui:AppButton Margin="4" Padding="4" Background="Red" />
                <ui:AppIconButton Width="24" />
                <ui:AppActionButton Padding="4" />
                <ui:AppIcon StrokeThickness="3" />
                <ui:AppCard CornerRadius="2" Padding="5" />
                <ui:AppScrollViewer Background="Red" />
                <ui:AppColorSwatch BorderBrush="Red" Width="24" />
                <ui:AppViewbox Height="80" />
                <ui:AppInput FontSize="20" />
                <ui:AppText Foreground="Red" />
                <ui:AppSelectableText LineHeight="18" />
                <ui:AppSettingsNavButton Padding="4" />
                <ui:SyntaxHighlightedSelectableTextBlock FontSize="12" />
              </Grid>
              <UserControl.Styles>
                <Style Selector="ui|AppButton"><Setter Property="Background" Value="Red" /></Style>
                <Style Selector="ui|AppButton"><Setter Property="Margin" Value="4" /></Style>
              </UserControl.Styles>
            </UserControl>
            """);

        Assert.Equal(["AppButton", "AppIconButton", "AppActionButton", "AppIcon", "AppCard", "AppScrollViewer",
            "AppColorSwatch", "AppViewbox", "AppInput", "AppText", "AppSelectableText", "AppSettingsNavButton",
            "SyntaxHighlightedSelectableTextBlock", "Setter"],
            document.XPathSelectElements(query).Select(element => element.Name.LocalName));
    }

    [Fact]
    public void BuildGuard_RejectsButtonAlignmentAndSizingOverrides()
    {
        var project = AppBuildDefinition.Load();
        var query = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedComponentAppearance")
            .Elements().Single(element => element.Name.LocalName == "XmlPeek")
            .Attribute("Query")?.Value;
        Assert.NotNull(query);

        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <Grid>
                <ui:AppButton HorizontalContentAlignment="Left" />
                <ui:AppIconButton VerticalContentAlignment="Top" />
                <ui:AppActionButton MinHeight="30" />
                <ui:AppSettingsNavButton MaxWidth="180" />
                <ui:AppButton Classes="align-leading" />
              </Grid>
            </UserControl>
            """);

        Assert.Equal(["AppButton", "AppIconButton", "AppActionButton", "AppSettingsNavButton"],
            document.XPathSelectElements(query).Select(element => element.Name.LocalName));
    }

    private static string[] FindInlineButtonAppearance(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName is "AppButton" or "AppIconButton" or "AppActionButton"
                or "AppSettingsNavButton")
            .SelectMany(element => element.Attributes()
                .Where(attribute => InlineButtonAppearanceProperties.Contains(attribute.Name.LocalName))
                .Select(attribute => attribute.Name.LocalName))
            .ToArray();
    }

    private static string[] FindInlineSurfaceAndFieldAppearance(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName is "AppCard" or "AppScrollViewer"
                    or "AppSlider" or "AppItemsControl" or "AppContentControl" or "AppColorSwatch" or "AppViewbox"
                || SharedFieldElements.Contains(element.Name.LocalName))
            .SelectMany(element => element.Attributes()
                .Where(attribute => (element.Name.LocalName is "AppCard" or "AppScrollViewer"
                        or "AppSlider" or "AppItemsControl" or "AppContentControl" or "AppColorSwatch"
                        ? InlineSurfaceAppearanceProperties : InlineFieldAppearanceProperties)
                    .Contains(attribute.Name.LocalName)
                    || element.Name.LocalName == "AppCard" && attribute.Name.LocalName == "Padding"
                    || (element.Name.LocalName is "AppColorSwatch" or "AppViewbox")
                        && (attribute.Name.LocalName is "Width" or "Height"))
                .Select(attribute => $"{element.Name.LocalName}.{attribute.Name.LocalName}"))
            .ToArray();
    }

    private static string[] FindInlineTextAppearance(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName is "AppText" or "AppSelectableText"
                or "MarkdownSelectableTextBlock" or "SyntaxHighlightedSelectableTextBlock")
            .SelectMany(element => element.Attributes()
                .Where(attribute => InlineTextAppearanceProperties.Contains(attribute.Name.LocalName))
                .Select(attribute => $"{element.Name.LocalName}.{attribute.Name.LocalName}"))
            .ToArray();
    }

    private static string[] FindLiteralColors(XDocument document)
    {
        return document.Descendants()
            .SelectMany(element => element.Attributes()
                .Where(attribute => Regex.IsMatch(attribute.Value, @"^#[0-9A-Fa-f]{3,8}$"))
                .Select(attribute => $"{element.Name.LocalName}.{attribute.Name.LocalName}"))
            .ToArray();
    }

    private static string[] FindLocalVisualSetters(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "Setter"
                && element.Ancestors().Any(ancestor => ancestor.Name.LocalName is "Style" or "ControlTheme")
                && SharedVisualStyleProperties.Contains(element.Attribute("Property")?.Value ?? ""))
            .Select(element => element.Attribute("Property")!.Value)
            .ToArray();
    }

    private static IEnumerable<string> VisualFiles() =>
        Directory.EnumerateFiles(
            Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Views"),
            "*.axaml", SearchOption.AllDirectories)
        .Concat(Directory.EnumerateFiles(
            Path.Combine(AppBuildDefinition.RepositoryRoot, "src", "MossAgent.App", "Controls"),
            "*.axaml", SearchOption.AllDirectories));

}
