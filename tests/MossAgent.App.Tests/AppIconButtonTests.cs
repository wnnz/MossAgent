using System.Xml.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Media;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppIconButtonTests
{
    [Fact]
    public void IconButton_OwnsIconAndAccessibleLabel()
    {
        var button = new AppIconButton { Label = "显示上下文" };
        var icon = Assert.IsType<AppIcon>(button.Content);

        Assert.Contains("ghost", button.Classes);
        Assert.Contains("icon", button.Classes);
        Assert.False(icon.IsHitTestVisible);
        Assert.DoesNotContain("icon-small", icon.Classes);
        Assert.Equal(14, icon.Size);
        Assert.Equal("显示上下文", AutomationProperties.GetName(button));
        Assert.Equal("显示上下文", ToolTip.GetTip(button));

        button.Foreground = Brushes.Red;
        Assert.Same(Brushes.Red, icon.Stroke);

        button.Label = "隐藏上下文";
        Assert.Equal("隐藏上下文", AutomationProperties.GetName(button));
        Assert.Equal("隐藏上下文", ToolTip.GetTip(button));
    }

    [Fact]
    public void IconButton_ChangesGeometryWithoutReplacingItsIcon()
    {
        var primary = new StreamGeometry();
        var alternate = new StreamGeometry();
        var button = new AppIconButton { IconData = primary, AlternateIconData = alternate };
        var icon = Assert.IsType<AppIcon>(button.Content);

        Assert.Same(primary, icon.Data);
        button.IsAlternateIcon = true;
        Assert.Same(alternate, icon.Data);
        button.IsAlternateIcon = false;
        Assert.Same(primary, icon.Data);
        button.AlternateIconData = null;
        button.IsAlternateIcon = true;
        Assert.Same(primary, icon.Data);
        Assert.Same(icon, button.Content);
    }

    [Fact]
    public void FilledIcon_TracksButtonForeground()
    {
        var button = new AppIconButton { Foreground = Brushes.Red, IsFilledIcon = true };
        var icon = Assert.IsType<AppIcon>(button.Content);

        Assert.Same(Brushes.Red, icon.Fill);
        Assert.Same(Brushes.Transparent, icon.Stroke);

        button.Foreground = Brushes.Blue;
        Assert.Same(Brushes.Blue, icon.Fill);
        button.IsFilledIcon = false;
        Assert.Same(Brushes.Transparent, icon.Fill);
        Assert.Same(Brushes.Blue, icon.Stroke);
    }

    [Fact]
    public void PlainIconActions_UseIconButtonInsteadOfManualComposition()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var manualButtons = FindManualIconButtons(XDocument.Load(file));

            Assert.True(manualButtons.Length == 0,
                $"{Path.GetFileName(file)} manually composes icon-only buttons");
        }
    }

    [Fact]
    public void IconOnlyPolicy_RejectsIconsNestedInLayoutContainers()
    {
        var document = XDocument.Parse("""
            <Grid xmlns="https://github.com/avaloniaui"
                  xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppButton><ui:AppIcon /></ui:AppButton>
              <ui:AppButton><Panel><ui:AppIcon /><ui:AppIcon /></Panel></ui:AppButton>
              <ui:AppButton><StackPanel><ui:AppIcon /><ui:AppText /></StackPanel></ui:AppButton>
              <ui:AppButton><ui:AppIconLabel /></ui:AppButton>
            </Grid>
            """);

        Assert.Equal(2, FindManualIconButtons(document).Length);
    }

    [Fact]
    public void IconOnlyActions_CannotUseGenericButton()
    {
        var views = Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Views");
        foreach (var file in Directory.EnumerateFiles(views, "*.axaml", SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            var manualButtons = document.Descendants()
                .Where(element => element.Name.LocalName == "AppButton")
                .Where(element => element.Attribute("Classes")?.Value.Split(' ')
                    .Any(value => value is "icon" or "window-control") == true)
                .ToArray();

            Assert.True(manualButtons.Length == 0,
                $"{Path.GetFileName(file)} uses a generic button for an icon-only action");
        }
    }

    [Fact]
    public void ExpandableIconButtons_UseOneSharedIconSize()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var style = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppButton.expand-icon controls|AppIcon.ui-icon");

        Assert.Contains(style.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Size"
            && element.Attribute("Value")?.Value == "12");
    }

    [Fact]
    public void WindowChrome_UsesAccessibleSharedIconButtons()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var window = XDocument.Load(Path.Combine(appPath, "Views", "MainWindow.axaml"));
        var buttons = window.Descendants()
            .Where(element => element.Attribute("Classes")?.Value.Split(' ')
                .Contains("window-control") == true)
            .ToArray();

        Assert.Equal(3, buttons.Length);
        Assert.All(buttons, button =>
        {
            Assert.Equal("AppIconButton", button.Name.LocalName);
            Assert.NotNull(button.Attribute("Label"));
            Assert.NotNull(button.Attribute("IconData"));
            Assert.Empty(button.Elements());
        });

        var maximize = Assert.Single(buttons, button => button.Attributes()
            .Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "MaximizeButton"));
        Assert.Equal("{StaticResource UiIconRestore}", maximize.Attribute("AlternateIconData")?.Value);
        Assert.Contains("window-maximize", maximize.Attribute("Classes")!.Value.Split(' '));
        var minimize = Assert.Single(buttons, button => button.Attribute("Label")?.Value == "最小化");
        Assert.Null(minimize.Attribute("IsFilledIcon"));

        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "WindowCaptionVariants.axaml"));
        var style = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppButton.window-control controls|AppIcon.ui-icon");
        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "Size"
            && element.Attribute("Value")?.Value == "8");
        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "StrokeThickness"
            && element.Attribute("Value")?.Value == "1");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var applicationStyles = application.Root!.Elements().Single(element => element.Name.LocalName == "Application.Styles").Elements().ToArray();
        var caption = Assert.Single(applicationStyles, element => element.Attribute("Source")?.Value
            == "avares://MossAgent.App/Styles/WindowCaptionVariants.axaml");
        foreach (var selector in new[] { "controls|AppButton.ghost", "controls|AppButton.ghost:pointerover", "controls|AppButton.closeButton:pointerover" })
            Assert.True(Array.IndexOf(applicationStyles, caption) > Array.IndexOf(applicationStyles,
                Assert.Single(applicationStyles, element => element.Attribute("Selector")?.Value == selector)));
    }

    [Theory]
    [InlineData("WindowCaptionVariants.axaml", "window-control", "8")]
    [InlineData("WindowVariants.axaml", "navigation-history:not(.titlebar-panel)", "9")]
    [InlineData("WindowVariants.axaml", "titlebar-panel", "10")]
    public void TitlebarGlyphSizesAndStrokes_BelongToSharedSemanticVariants(string stylesFile, string variant, string size)
    {
        var styles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", stylesFile));
        var style = Assert.Single(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == $"controls|AppButton.{variant} controls|AppIcon.ui-icon");

        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "Size"
            && element.Attribute("Value")?.Value == size);
        Assert.Contains(style.Elements(), element => element.Attribute("Property")?.Value == "StrokeThickness"
            && element.Attribute("Value")?.Value == "1");
    }

    [Fact]
    public void IconSizeUpdatesItsSquareLayoutSlotWithoutDependingOnGeometryAspectRatio()
    {
        var icon = new AppIcon { Data = new RectangleGeometry(new Avalonia.Rect(4, 6, 16, 12)), Size = 12 };

        Assert.Equal(12, icon.Width);
        Assert.Equal(12, icon.Height);
        icon.Size = 8;
        Assert.Equal(8, icon.Width);
        Assert.Equal(8, icon.Height);
    }

    [Fact]
    public void AlternateGlyphStateIsExposedToSharedVariantsAndReusesTheIcon()
    {
        var normal = new RectangleGeometry(new Avalonia.Rect(0, 0, 10, 10));
        var alternate = new RectangleGeometry(new Avalonia.Rect(1, 1, 8, 8));
        var button = new AppIconButton { IconData = normal, AlternateIconData = alternate };
        var icon = Assert.IsType<AppIcon>(button.Content);
        foreach (var selected in new[] { false, true, false })
        {
            button.IsAlternateIcon = selected;
            Assert.Equal(selected, button.Classes.Contains(":alternate"));
            Assert.Same(selected ? alternate : normal, icon.Data);
            Assert.Same(icon, button.Content);
        }
    }

    private static XElement[] FindManualIconButtons(XDocument document) => document.Descendants()
        .Where(element => element.Name.LocalName == "AppButton" && element.Attribute("Content") is null)
        .Where(element =>
        {
            var content = element.Elements()
                .Where(child => !child.Name.LocalName.Contains('.'))
                .SelectMany(child => child.DescendantsAndSelf())
                .ToArray();
            return content.Any(child => child.Name.LocalName == "AppIcon")
                && !content.Any(child => child.Name.LocalName is "AppText" or "AppIconLabel");
        })
        .ToArray();

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
