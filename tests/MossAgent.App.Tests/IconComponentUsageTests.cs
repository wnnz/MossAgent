using System.Xml.Linq;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class IconComponentUsageTests
{
    [Fact]
    public void Axaml_UsesSharedIconAppearance()
    {
        foreach (var file in VisualFiles())
        {
            var document = XDocument.Load(file);
            var iconAppearanceOverrides = FindInlineIconAppearance(document);
            Assert.True(iconAppearanceOverrides.Length == 0,
                $"{Path.GetFileName(file)} overrides shared icon appearance: "
                + string.Join(", ", iconAppearanceOverrides));

            var iconButtonOverrides = document.Descendants()
                .Where(element => IsIconButton(element)
                    && (element.Attribute("Width") is not null
                        || element.Attribute("Height") is not null
                        || element.Attribute("Padding") is not null
                        || element.Attribute("FontSize") is not null))
                .ToArray();
            Assert.True(iconButtonOverrides.Length == 0,
                $"{Path.GetFileName(file)} sets icon-button dimensions outside shared size classes");
        }
    }

    [Fact]
    public void IconAppearancePolicy_RejectsInlineStrokeAndSize()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppIcon Size="20" StrokeThickness="3" />
            </UserControl>
            """);

        Assert.Equal(["Size", "StrokeThickness"], FindInlineIconAppearance(document));
    }

    [Fact]
    public void Axaml_IconButtonsUseSharedIconGeometry()
    {
        foreach (var file in VisualFiles())
        {
            var offenders = FindIconButtonsWithoutSharedGeometry(XDocument.Load(file));
            Assert.True(offenders.Length == 0,
                $"{Path.GetFileName(file)} uses text glyphs or missing geometry in icon buttons: "
                + string.Join(", ", offenders));
        }
    }

    [Fact]
    public void IconButtonPolicy_RejectsTextGlyphs()
    {
        var document = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui"
                         xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppButton Classes="icon" Content="×" />
              <ui:AppButton Classes="window-control" />
            </UserControl>
            """);

        Assert.Equal(["×", "missing AppIcon"], FindIconButtonsWithoutSharedGeometry(document));
    }

    [Fact]
    public void Axaml_DropdownChevronsUseSharedGeometry()
    {
        foreach (var file in VisualFiles())
        {
            var textChevrons = XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName == "AppText"
                    && element.Attribute("Text")?.Value == "⌄")
                .ToArray();
            Assert.True(textChevrons.Length == 0,
                $"{Path.GetFileName(file)} uses a font-dependent chevron glyph");
        }
    }

    [Fact]
    public void RunningComposerAndToolDisclosureUseSharedGeometry()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var composer = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceComposerView.axaml"));
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceMessageView.axaml"));
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));

        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StreamGeometry"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "UiIconStop"));
        var sendButton = Assert.Single(composer.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "SendButton"));
        var stopButton = Assert.Single(composer.Descendants(), element => element.Name.LocalName == "AppIconButton"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Name" && attribute.Value == "StopButton"));
        Assert.Equal("{StaticResource UiIconSend}", sendButton.Attribute("IconData")?.Value);
        Assert.Equal("{Binding ComposerRouter.SubmitCommand}", sendButton.Attribute("Command")?.Value);
        Assert.Equal("{Binding !IsRunning}", sendButton.Attribute("IsVisible")?.Value);
        Assert.Equal("{StaticResource UiIconStop}", stopButton.Attribute("IconData")?.Value);
        Assert.Equal("{Binding PrimaryActionCommand}", stopButton.Attribute("Command")?.Value);
        Assert.Equal("{Binding IsRunning}", stopButton.Attribute("IsVisible")?.Value);

        var disclosure = Assert.Single(chat.Descendants(), element => element.Name.LocalName == "AppToolActivityRow"
            && element.Attribute("Command")?.Value == "{Binding ToggleToolCommand}");
        Assert.Equal("{Binding IsToolExpanded}", disclosure.Attribute("IsExpanded")?.Value);
        var sharedRow = XDocument.Load(Path.Combine(appPath, "Controls", "AppToolActivityRow.axaml"));
        Assert.Contains(sharedRow.Descendants(), element => element.Name.LocalName == "AppIcon"
            && element.Attribute("Data")?.Value == "{DynamicResource UiIconChevronDown}"
            && element.Attribute("IsVisible")?.Value == "{Binding IsExpanded}");
        Assert.Contains(sharedRow.Descendants(), element => element.Name.LocalName == "AppIcon"
            && element.Attribute("Data")?.Value == "{DynamicResource UiIconChevronRight}"
            && element.Attribute("IsVisible")?.Value == "{Binding !IsExpanded}");
        Assert.DoesNotContain(sharedRow.Descendants(), element => element.Attribute("Text")?.Value
            == "{Binding ExpandGlyph}");
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|AppButton.composer-send controls|AppIcon.ui-icon");
    }

    private static string[] FindInlineIconAppearance(XDocument document)
    {
        return document.Descendants()
            .Where(element => element.Name.LocalName == "AppIcon")
            .SelectMany(element => element.Attributes()
                .Where(attribute => attribute.Name.LocalName is "Width" or "Height" or "Size"
                    or "Stroke" or "Fill" or "StrokeThickness")
                .Select(attribute => attribute.Name.LocalName))
            .ToArray();
    }

    private static string[] FindIconButtonsWithoutSharedGeometry(XDocument document)
    {
        return document.Descendants()
            .Where(IsIconButton)
            .Where(element => element.Attribute("Content") is not null
                || !element.Descendants().Any(child => child.Name.LocalName == "AppIcon"))
            .Select(element => element.Attribute("Content")?.Value ?? "missing AppIcon")
            .ToArray();
    }

    private static bool IsIconButton(XElement element) =>
        element.Name.LocalName == "AppButton"
        && element.Attribute("Classes")?.Value.Split(' ')
            .Any(value => value is "icon" or "window-control") == true;

    private static IEnumerable<string> VisualFiles()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        return Directory.EnumerateFiles(Path.Combine(appPath, "Views"), "*.axaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(appPath, "Controls"), "*.axaml", SearchOption.AllDirectories));
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
