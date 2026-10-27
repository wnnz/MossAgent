using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia.Media;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SharedSurfaceShadowTests
{
    [Fact]
    public void SurfaceShadowToken_IsLightAndSymmetric()
    {
        var document = XDocument.Load(Path.Combine(AppPath(), "App.axaml"));
        var token = Assert.Single(document.Descendants(), element => element.Name.LocalName == "BoxShadows"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "SharedSurfaceShadow"));
        var shadows = BoxShadows.Parse(token.Value);

        Assert.Equal(1, shadows.Count);
        var shadow = shadows[0];
        Assert.Equal(0, shadow.OffsetX);
        Assert.Equal(0, shadow.OffsetY);
        Assert.Equal(14, shadow.Blur);
        Assert.Equal(0, shadow.Spread);
        Assert.InRange(shadow.Color.A, (byte)1, (byte)16);
        Assert.False(shadow.IsInset);
    }

    [Fact]
    public void CardsAndDropdowns_UseTheSameShadowToken()
    {
        foreach (var (file, selector) in new[]
        {
            ("Styles/AppButtonVariants.axaml", "controls|AppCard"),
            ("Styles/ComboBoxVariants.axaml", ".app-combo-box /template/ Border#PopupBorder"),
            ("Styles/AttachmentChipVariants.axaml", "controls|AppBorder.attachment-chip-surface")
        })
        {
            var document = XDocument.Load(Path.Combine(AppPath(), file));
            var style = Assert.Single(document.Descendants(), element => element.Attribute("Selector")?.Value == selector);
            var shadow = Assert.Single(style.Elements(), element => element.Attribute("Property")?.Value == "BoxShadow");
            Assert.Equal("{StaticResource SharedSurfaceShadow}", shadow.Attribute("Value")?.Value);
        }
    }

    [Fact]
    public void WindowFrame_UsesSymmetricMeasuredShadowWithoutChangingCardShadows()
    {
        var document = XDocument.Load(Path.Combine(AppPath(), "App.axaml"));
        var token = Assert.Single(document.Descendants(), element => element.Name.LocalName == "BoxShadows"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "SharedWindowFrameShadow"));
        var shadows = BoxShadows.Parse(token.Value);
        Assert.Equal(2, shadows.Count);
        for (var index = 0; index < shadows.Count; index++)
        {
            var shadow = shadows[index];
            Assert.Equal(0, shadow.OffsetX);
            Assert.Equal(0, shadow.OffsetY);
            Assert.False(shadow.IsInset);
            Assert.InRange(shadow.Color.A, (byte)1, (byte)16);
        }
        Assert.Equal(28, shadows[0].Blur);
        Assert.Equal(0, shadows[0].Spread);
        Assert.Equal(0, shadows[1].Blur);
        Assert.Equal(1, shadows[1].Spread);
        var style = XDocument.Load(Path.Combine(AppPath(), "Styles", "WindowVariants.axaml"))
            .Descendants().Single(element => element.Attribute("Selector")?.Value == "controls|AppBorder.window-content-frame");
        Assert.Equal("{StaticResource SharedWindowFrameShadow}", Assert.Single(style.Elements(),
            element => element.Attribute("Property")?.Value == "BoxShadow").Attribute("Value")?.Value);
    }

    [Theory]
    [InlineData("<Setter Property='BoxShadow' Value='0 4 16 0 #44000000' />", true)]
    [InlineData("<Setter Property='BoxShadow' Value='{StaticResource OtherShadow}' />", true)]
    [InlineData("<Setter Property='BoxShadow' Value='{StaticResource SharedWindowFrameShadow}' />", true)]
    [InlineData("<Setter Property='BoxShadow'><BoxShadows>0 4 16 0 #44000000</BoxShadows></Setter>", true)]
    [InlineData("<Setter Property='BoxShadow' Value='{StaticResource SharedSurfaceShadow}' />", false)]
    [InlineData("<Setter Property='BoxShadow' Value='None' />", false)]
    [InlineData("<Setter Property='CornerRadius' Value='12' />", false)]
    public void BuildGuard_RejectsIndependentShadows(string markup, bool rejected)
    {
        var document = XDocument.Parse("<Styles xmlns='https://github.com/avaloniaui'><Style Selector='Border'>"
            + markup + "</Style></Styles>");

        Assert.Equal(rejected, document.XPathSelectElements(GuardQuery()).Any());
    }

    [Theory]
    [InlineData("controls|AppBorder.window-content-frame", false)]
    [InlineData("controls|AppCard", true)]
    [InlineData("controls|AppBorder.window-content-clip", true)]
    [InlineData("Border.window-content-frame", true)]
    public void BuildGuard_RestrictsWindowShadowToSharedFrame(string selector, bool rejected)
    {
        var document = XDocument.Parse("<Styles xmlns='https://github.com/avaloniaui'><Style Selector='"
            + selector + "'><Setter Property='BoxShadow' Value='{StaticResource SharedWindowFrameShadow}' /></Style></Styles>");
        Assert.Equal(rejected, document.XPathSelectElements(GuardQuery()).Any());
    }

    [Fact]
    public void AllSharedStyles_UseTheShadowToken()
    {
        var files = Directory.EnumerateFiles(Path.Combine(AppPath(), "Styles"), "*.axaml", SearchOption.AllDirectories)
            .Append(Path.Combine(AppPath(), "App.axaml"));
        foreach (var file in files)
            Assert.True(!XDocument.Load(file).XPathSelectElements(GuardQuery()).Any(),
                $"{Path.GetFileName(file)} defines an independent surface shadow.");
    }

    private static string GuardQuery()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants("Target"), element =>
            element.Attribute("Name")?.Value == "ValidateSharedSurfaceShadows");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        return Assert.IsType<string>(Assert.Single(target.Elements("XmlPeek")).Attribute("Query")?.Value);
    }

    private static string AppPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return Path.Combine(directory.FullName, "src", "MossAgent.App");
    }
}
