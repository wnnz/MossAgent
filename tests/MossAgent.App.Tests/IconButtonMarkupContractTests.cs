using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class IconButtonMarkupContractTests
{
    [Fact]
    public void IconButtons_DeclareGeometryThroughSemanticDataOrSharedAppearanceAndRetainLabels()
    {
        var app = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var styles = Directory.EnumerateFiles(Path.Combine(app, "Styles"), "*.axaml", SearchOption.AllDirectories)
            .Append(Path.Combine(app, "App.axaml")).Select(XDocument.Load).ToArray();
        var variants = ReadIconVariants(styles);
        foreach (var file in Directory.EnumerateFiles(Path.Combine(app, "Views"), "*.axaml", SearchOption.AllDirectories))
        {
            foreach (var button in XDocument.Load(file).Descendants().Where(element => element.Name.LocalName == "AppIconButton"))
            {
                Assert.True(HasIconData(button, variants), $"{Path.GetFileName(file)} has an icon button without semantic or shared geometry.");
                Assert.NotNull(button.Attribute("Label"));
                Assert.Null(button.Attribute("Content"));
                Assert.DoesNotContain(button.Elements(), element => !element.Name.LocalName.Contains('.'));
            }
        }
    }

    [Theory]
    [InlineData("controls|AppButton.header-icon", "controls:AppIconButton", "{DynamicResource HeaderIcon}", "HeaderIcon", true)]
    [InlineData("controls|AppButton.header-icon", "controls:AppIconButton", "{StaticResource HeaderIcon}", "HeaderIcon", true)]
    [InlineData("controls|AppButton.header-icon:pointerover", "controls:AppIconButton", "{DynamicResource HeaderIcon}", "HeaderIcon", false)]
    [InlineData("controls|AppButton.header-icon", "controls:AppActionButton", "{DynamicResource HeaderIcon}", "HeaderIcon", false)]
    [InlineData("controls|AppButton.header-icon", "controls:AppIconButton", "{DynamicResource MissingIcon}", "HeaderIcon", false)]
    [InlineData("controls|AppButton.header-icon", "controls:AppIconButton", "{Binding IconData}", "HeaderIcon", false)]
    public void SharedIconVariants_RequireUnconditionalTypedStylesAndAnExistingGeometry(
        string selector, string target, string value, string resource, bool expected)
    {
        var document = XDocument.Parse($$"""
            <Styles xmlns="https://github.com/avaloniaui" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
              <Styles.Resources><StreamGeometry x:Key="{{resource}}">M 0 0 L 1 1</StreamGeometry></Styles.Resources>
              <Style Selector="{{selector}}" x:SetterTargetType="{{target}}">
                <Setter Property="IconData" Value="{{value}}" />
              </Style>
            </Styles>
            """);
        var variants = ReadIconVariants([document]);
        Assert.Equal(expected, HasIconData(XElement.Parse("<AppIconButton Classes='ghost header-icon' />"), variants));
        Assert.False(HasIconData(XElement.Parse("<AppIconButton Classes='ghost unknown' />"), variants));
        Assert.False(HasIconData(XElement.Parse("<AppIconButton />"), variants));
    }

    private static bool HasIconData(XElement button, IReadOnlySet<string> variants) =>
        !string.IsNullOrWhiteSpace(button.Attribute("IconData")?.Value)
        || (button.Attribute("Classes")?.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Any(variants.Contains) ?? false);

    private static HashSet<string> ReadIconVariants(IEnumerable<XDocument> documents)
    {
        XNamespace avalonia = "https://github.com/avaloniaui";
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
        var snapshot = documents.ToArray();
        var geometryKeys = snapshot.SelectMany(document => document.Descendants(avalonia + "StreamGeometry"))
            .Select(element => element.Attribute(xaml + "Key")?.Value).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var variants = new HashSet<string>(StringComparer.Ordinal);
        const string prefix = "controls|AppButton.";
        foreach (var style in snapshot.SelectMany(document => document.Descendants(avalonia + "Style")))
        {
            var selector = style.Attribute("Selector")?.Value;
            if (style.Attribute(xaml + "SetterTargetType")?.Value != "controls:AppIconButton"
                || selector?.StartsWith(prefix, StringComparison.Ordinal) != true) continue;
            var variant = selector[prefix.Length..];
            if (variant.Length == 0 || !variant.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')) continue;
            var value = style.Elements(avalonia + "Setter").FirstOrDefault(setter =>
                setter.Attribute("Property")?.Value == "IconData")?.Attribute("Value")?.Value;
            if (value is null || !value.EndsWith('}') || !(value.StartsWith("{DynamicResource ", StringComparison.Ordinal)
                || value.StartsWith("{StaticResource ", StringComparison.Ordinal))) continue;
            if (geometryKeys.Contains(value[(value.IndexOf(' ') + 1)..^1])) variants.Add(variant);
        }
        return variants;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
