using System.Xml.Linq;
using Avalonia.Controls;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class CheckBoxComponentTests
{
    [Fact]
    public void CheckBoxRetainsNativeSelectionAndThreeStateBehavior()
    {
        var checkbox = new AppCheckBox { Content = "Enabled", IsChecked = true };

        Assert.IsAssignableFrom<CheckBox>(checkbox);
        Assert.True(checkbox.IsChecked);
        checkbox.IsChecked = false;
        Assert.False(checkbox.IsChecked);
        checkbox.IsThreeState = true;
        checkbox.IsChecked = null;
        Assert.Null(checkbox.IsChecked);
        Assert.Contains("app-check-box", checkbox.Classes);
    }

    [Fact]
    public void SharedCheckBoxTemplateUsesThemeBrushesAndCenteredGeometry()
    {
        var styles = Styles();
        var surface = Assert.Single(styles.Descendants(), element =>
            element.Name.LocalName == "AppBorder" && Name(element) == "PART_CheckSurface");
        var glyph = Assert.Single(styles.Descendants(), element =>
            element.Name.LocalName == "AppIcon" && Name(element) == "PART_CheckGlyph");

        Assert.Equal("16", surface.Attribute("Width")?.Value);
        Assert.Equal("16", surface.Attribute("Height")?.Value);
        Assert.Equal("12", glyph.Attribute("Size")?.Value);
        Assert.Equal("False", glyph.Attribute("IsHitTestVisible")?.Value);
        Assert.Equal("Center", glyph.Attribute("VerticalAlignment")?.Value);
        Assert.Equal("{DynamicResource PrimaryButtonForegroundBrush}", glyph.Attribute("Stroke")?.Value);
        var application = XDocument.Load(Path.Combine(AppPath(), "App.axaml"));
        Assert.Contains(application.Descendants(), element => element.Attribute("Source")?.Value
            == "avares://MossAgent.App/Styles/CheckBoxVariants.axaml");
    }

    [Theory]
    [InlineData(":checked", "AppBorder#PART_CheckSurface", "Background", "{DynamicResource PrimaryButtonBackgroundBrush}")]
    [InlineData(":checked", "AppIcon#PART_CheckGlyph", "IsVisible", "True")]
    [InlineData(":indeterminate", "AppIcon#PART_CheckGlyph", "IsVisible", "True")]
    [InlineData(":pointerover", "AppBorder#PART_CheckSurface", "Opacity", "0.8")]
    [InlineData(":pressed", "AppBorder#PART_CheckSurface", "Opacity", "0.65")]
    [InlineData(":focus-visible", "AppBorder#PART_CheckSurface", "BorderThickness", "2")]
    [InlineData(":disabled", "", "Opacity", "0.45")]
    public void SharedCheckBoxTemplateOwnsInteractionStates(string state, string part, string property, string value)
    {
        var selector = "^" + state + (part.Length > 0 ? " /template/ ui|" + part : string.Empty);
        var style = Assert.Single(Styles().Descendants(), element => element.Attribute("Selector")?.Value == selector);

        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == property
            && element.Attribute("Value")?.Value == value);
    }

    private static string? Name(XElement element) => element.Attribute(
        XName.Get("Name", "http://schemas.microsoft.com/winfx/2006/xaml"))?.Value;

    private static XDocument Styles() => XDocument.Load(Path.Combine(AppPath(), "Styles", "CheckBoxVariants.axaml"));

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
