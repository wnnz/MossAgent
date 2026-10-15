using System.Xml.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppIconLabelTests
{
    [Fact]
    public void StandaloneLabel_UsesSharedCenteredIconAndTruncatedText()
    {
        var layout = new AppIconLabel
        {
            IconData = new RectangleGeometry(),
            Label = "项目名称",
            LabelMaxWidth = 120
        };

        Assert.Equal(GridLength.Auto, layout.ColumnDefinitions[0].Width);
        Assert.Equal(GridLength.Star, layout.ColumnDefinitions[1].Width);
        var icon = Assert.IsType<AppIcon>(layout.Children[0]);
        var text = Assert.IsType<AppText>(layout.Children[1]);
        Assert.Equal(layout.IconData, icon.Data);
        Assert.Equal(layout.Label, text.Text);
        Assert.Equal(layout.LabelMaxWidth, text.MaxWidth);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.Equal(VerticalAlignment.Center, icon.VerticalAlignment);
        Assert.Equal(VerticalAlignment.Center, text.VerticalAlignment);
        Assert.Equal(HorizontalAlignment.Left, text.HorizontalAlignment);
        Assert.False(icon.IsHitTestVisible);
    }

    [Theory]
    [InlineData("controls|AppIconLabel", "5")]
    [InlineData("controls|AppIconLabel.owned-icon-label", "9")]
    public void IconLabelSpacing_ComesFromSharedStyles(string selector, string spacing)
    {
        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var style = Assert.Single(styles.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == selector);

        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "Spacing"
            && element.Attribute("Value")?.Value == spacing);
    }

    [Fact]
    public void MetadataRows_UseSharedIconLabelVariants()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var modelPicker = XDocument.Load(Path.Combine(appPath, "Controls", "AppModelPickerMenu.axaml"));
        var sidebar = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceSidebar.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));

        Assert.Contains(modelPicker.Descendants(), element => element.Name.LocalName == "AppIconLabel"
            && element.Attribute("Classes")?.Value == "model-provider-label"
            && element.Attribute("Label")?.Value == "{Binding ProviderName}");
        foreach (var variant in new[] { "project-summary-title", "project-summary-path" })
        {
            var label = Assert.Single(sidebar.Descendants(), element => element.Name.LocalName == "AppIconLabel"
                && element.Attribute("Classes")?.Value == variant);
            Assert.Null(label.Attribute("LabelMaxWidth"));
            var sharedStyle = Assert.Single(styles.Descendants(), element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value.Split(", ")
                    .Contains("controls|AppIconLabel." + variant) == true);
            Assert.Contains(sharedStyle.Elements(), element => element.Attribute("Property")?.Value == "LabelMaxWidth"
                && element.Attribute("Value")?.Value == "260");
        }

        foreach (var selector in new[]
        {
            "controls|AppIconLabel.model-provider-label",
            "controls|AppIconLabel.model-provider-label controls|AppIcon",
            "controls|AppIconLabel.model-provider-label controls|AppText",
            "controls|AppIconLabel.project-summary-title, controls|AppIconLabel.project-summary-path",
            "controls|AppIconLabel.project-summary-title controls|AppText",
            "controls|AppIconLabel.project-summary-path controls|AppText"
        })
        {
            Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == selector);
        }
    }

    [Fact]
    public void ComposerContextLabels_UseSharedGeometryAndTypography()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var composer = XDocument.Load(Path.Combine(appPath, "Controls", "AppComposerContextBar.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var labels = composer.Descendants().Where(element => element.Name.LocalName == "AppIconLabel"
            && element.Attribute("Classes")?.Value.StartsWith("composer-context-label", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Single(labels);
        Assert.All(labels, label =>
        {
            Assert.NotNull(label.Attribute("IconData"));
            Assert.NotNull(label.Attribute("Label"));
            Assert.Null(label.Attribute("LabelMaxWidth"));
        });
        var actions = composer.Descendants().Where(element => element.Name.LocalName == "AppActionButton"
            && element.Attribute("Classes")?.Value.Contains("composer-context-button", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Equal(2, actions.Length);
        Assert.All(actions, action =>
        {
            Assert.NotNull(action.Attribute("IconData"));
            Assert.NotNull(action.Attribute("Label"));
        });
        foreach (var selector in new[]
        {
            "controls|AppIconLabel.composer-context-label",
            "controls|AppIconLabel.composer-context-label controls|AppIcon",
            "controls|AppIconLabel.composer-context-label controls|AppText",
            "controls|AppIconLabel.composer-context-label.muted controls|AppText"
        })
        {
            Assert.Contains(styles.Descendants(), element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value == selector);
        }
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
