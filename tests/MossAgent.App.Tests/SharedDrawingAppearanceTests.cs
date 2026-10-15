using System.Xml.Linq;
using System.Xml.XPath;
using MossAgent.App.Analyzers;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedDrawingAppearanceTests
{
    [Theory]
    [InlineData("icon.RenderTransform = new Avalonia.Media.TranslateTransform(0, 2);")]
    [InlineData("icon.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);")]
    [InlineData("icon.SetValue(Avalonia.Visual.RenderTransformProperty, new Avalonia.Media.ScaleTransform(2, 2));")]
    [InlineData("icon.Bind(Avalonia.Visual.RenderTransformProperty, new Avalonia.Data.Binding(\"Offset\"));")]
    [InlineData("icon.StrokeLineCap = Avalonia.Media.PenLineCap.Square;")]
    [InlineData("icon.StrokeJoin = Avalonia.Media.PenLineJoin.Bevel;")]
    [InlineData("icon.SetValue(MossAgent.App.Controls.AppIcon.StrokeMiterLimitProperty, 20d);")]
    [InlineData("icon.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 1d, 2d };")]
    [InlineData("icon.StrokeDashOffset = 2;")]
    [InlineData("icon.StrokeDashArray!.Add(2d);")]
    [InlineData("var dashes = icon.StrokeDashArray!; dashes.Clear();")]
    [InlineData("var dashes = icon.StrokeDashArray!; dashes[0] = 2d;")]
    [InlineData("var dashes = (icon.StrokeDashArray!); dashes.Clear();")]
    [InlineData("var dashes = (System.Collections.Generic.IList<double>)icon.StrokeDashArray!; dashes.Clear();")]
    public async Task BusinessCode_CannotOverrideSharedDrawing(string statement)
    {
        var source = "namespace MossAgent.App.Views { public class Host { "
            + "public void Render(MossAgent.App.Controls.AppIcon icon) { " + statement + " } } }";

        var diagnostic = Assert.Single(await VisualPolicyTestCompilation.Analyze(source,
            new SharedControlAppearanceAnalyzer()));

        Assert.Equal(SharedControlAppearanceAnalyzer.DiagnosticId, diagnostic.Id);
    }

    [Theory]
    [InlineData("<ui:AppIcon RenderTransform='translate(0, 2)' />")]
    [InlineData("<ui:AppIcon Visual.RenderTransformOrigin='0%,0%' />")]
    [InlineData("<ui:AppIcon StrokeLineCap='Square' />")]
    [InlineData("<ui:AppIcon StrokeJoin='Bevel' />")]
    [InlineData("<ui:AppIcon StrokeMiterLimit='20' />")]
    [InlineData("<ui:AppIcon StrokeDashArray='1,2' />")]
    [InlineData("<ui:AppIcon StrokeDashOffset='2' />")]
    [InlineData("<ui:AppIcon><ui:AppIcon.RenderTransform><TranslateTransform Y='2' /></ui:AppIcon.RenderTransform></ui:AppIcon>")]
    [InlineData("<ui:AppIcon><Shape.StrokeLineCap>Square</Shape.StrokeLineCap></ui:AppIcon>")]
    [InlineData("<ui:AppIconButton RenderTransform='scale(2)' />")]
    public void Markup_CannotOverrideSharedDrawing(string markup)
    {
        var document = XDocument.Parse("<Grid xmlns='https://github.com/avaloniaui' "
            + "xmlns:ui='clr-namespace:MossAgent.App.Controls'>" + markup + "</Grid>");

        Assert.Single(document.XPathSelectElements(DrawingQuery()));
    }

    [Fact]
    public async Task SharedImplementation_CanDefineDrawing()
    {
        var source = """
            namespace MossAgent.App.Controls
            {
                public class Drawing
                {
                    public void Configure(AppIcon icon)
                    {
                        icon.RenderTransform = new Avalonia.Media.TranslateTransform(0, 2);
                        icon.StrokeLineCap = Avalonia.Media.PenLineCap.Round;
                        icon.StrokeDashArray = new Avalonia.Collections.AvaloniaList<double> { 1d, 2d };
                        icon.StrokeDashArray.Add(3d);
                    }
                }
            }
            """;

        Assert.Empty(await VisualPolicyTestCompilation.Analyze(source, new SharedControlAppearanceAnalyzer()));
    }

    private static string DrawingQuery()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);

        var project = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App", "MossAgent.App.csproj"));
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedAppearanceTokens");
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }
}
