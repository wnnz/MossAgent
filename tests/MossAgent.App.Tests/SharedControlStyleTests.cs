using System.Xml.Linq;
using Avalonia.Controls;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SharedControlStyleTests
{
    [Fact]
    public void AppFlyout_UsesSharedPresenterStyle()
    {
        var flyout = new AppFlyout();

        Assert.Contains("app-flyout", flyout.FlyoutPresenterClasses);
    }

    [Fact]
    public void Application_DeclaresSharedControlThemes()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var themes = XDocument.Load(Path.Combine(appPath, "Styles", "ComponentVariants.axaml"));
        var themeKeys = themes.Root!.Elements()
            .Select(element => element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value)
            .ToArray();

        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "ResourceInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/ComponentVariants.axaml");
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/SurfaceVariants.axaml");
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/ThemePaletteVariants.axaml");
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/TypographyVariants.axaml");
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/ThemePreviewVariants.axaml");
        Assert.Contains("HistoryNavigationButtonTheme", themeKeys);
        Assert.Contains("WindowControlButtonTheme", themeKeys);
        Assert.Contains("SettingsNavTheme", themeKeys);
    }

    [Fact]
    public void DarkTheme_UsesReferenceShellSurfaceColors()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var dark = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "Dark"));
        var colors = dark.Elements()
            .Where(element => element.Name.LocalName == "SolidColorBrush")
            .ToDictionary(element => element.Attributes().Single(attribute => attribute.Name.LocalName == "Key").Value,
                element => element.Attribute("Color")!.Value);

        Assert.Equal("#181818", colors["AppBackgroundBrush"]);
        Assert.Equal("#1A2228", colors["HeaderBackgroundBrush"]);
        Assert.Equal("#1A2229", colors["SidebarBackgroundBrush"]);
        Assert.Equal("#2C333A", colors["SettingsSelectionBrush"]);
    }

    [Fact]
    public void LightTheme_UsesReferenceProjectSelectionColor()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var light = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "Light"));
        var selection = Assert.Single(light.Elements(), element => element.Name.LocalName == "SolidColorBrush"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "SettingsSelectionBrush"));

        Assert.Equal("#ECEFF1", selection.Attribute("Color")?.Value);
    }

    [Fact]
    public void Cards_UseSharedBorderAndSubtleFourSidedShadow()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var card = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppCard");
        Assert.Contains(card.Elements(), element => element.Attribute("Property")?.Value == "BorderBrush"
            && element.Attribute("Value")?.Value == "{DynamicResource CardBorderBrush}");
        Assert.Contains(card.Elements(), element => element.Attribute("Property")?.Value == "BoxShadow"
            && element.Attribute("Value")?.Value == "{StaticResource SharedSurfaceShadow}");
        foreach (var (theme, color) in new[] { ("Light", "#EDEDED"), ("Dark", "#424242") })
        {
            var palette = Assert.Single(document.Descendants(), element => element.Name.LocalName == "ResourceDictionary"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == theme));
            Assert.Contains(palette.Elements(), element => element.Name.LocalName == "SolidColorBrush"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "CardBorderBrush")
                && element.Attribute("Color")?.Value == color);
        }
    }

    [Fact]
    public void FluentBackedWrappersExposeSharedStyleClasses()
    {
        (Control Control, string StyleClass)[] wrappers =
        [
            (new AppInput(), "app-input"),
            (new AppComboBox(), "app-combo-box"),
            (new AppListBox(), "app-list-box"),
            (new AppCheckBox(), "app-check-box"),
            (new AppToggleButton(), "app-toggle-button"),
            (new AppExpander(), "app-expander"),
            (new AppNumericUpDown(), "app-numeric-up-down"),
            (new AppScrollViewer(), "app-scroll-viewer")
        ];

        foreach (var (control, styleClass) in wrappers)
        {
            Assert.Contains(styleClass, control.Classes);
        }
    }

    [Fact]
    public void SharedStylesUseClassesForFluentBackedWrappers()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var comboStyles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "ComboBoxVariants.axaml"));
        var selectors = document.Descendants().Concat(comboStyles.Descendants())
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .Where(value => value is not null)
            .ToArray();

        Assert.Contains(".app-input", selectors);
        Assert.Contains(".app-combo-box", selectors);
        Assert.Contains(document.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/ComboBoxVariants.axaml");
        Assert.Contains(".app-list-box", selectors);
        Assert.Contains(".app-check-box", selectors);
        Assert.Contains(".app-toggle-button", selectors);
        Assert.Contains(".app-numeric-up-down", selectors);
        Assert.DoesNotContain(selectors, selector => selector!.StartsWith("controls|AppInput", StringComparison.Ordinal));
        var surface = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var surfaceSelectors = surface.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToArray();
        foreach (var variant in new[] { "button-xs", "button-xxs", "button-sm", "menu-trigger", "composer-send" })
        {
            Assert.Contains($"controls|AppButton.{variant}", surfaceSelectors);
        }

        var themes = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "Styles", "ComponentVariants.axaml"));
        Assert.Contains(themes.Root!.Elements(), element => element.Name.LocalName == "ControlTheme"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == "SettingsNavTheme")
            && element.Attribute("TargetType")?.Value == "ToggleButton");
    }

    [Fact]
    public void TitlebarMenus_UseSharedMutedForeground()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        foreach (var (themeName, color) in new[] { ("Light", "#85898D"), ("Dark", "#A9AFB4") })
        {
            var theme = Assert.Single(application.Descendants(), element =>
                element.Name.LocalName == "ResourceDictionary"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                    && attribute.Value == themeName));
            Assert.Contains(theme.Elements(), element => element.Name.LocalName == "SolidColorBrush"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                    && attribute.Value == "TitlebarMenuForegroundBrush")
                && element.Attribute("Color")?.Value == color);
        }

        var variants = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var menuStyle = Assert.Single(variants.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppButton.ghost.menu-trigger");
        Assert.Contains(menuStyle.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Foreground"
            && element.Attribute("Value")?.Value == "{DynamicResource TitlebarMenuForegroundBrush}");
    }

    [Fact]
    public void SharedButtonVariantsOwnTheirPadding()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        (string File, string Selector, string Padding, string Variant)[] variants =
        [
            (Path.Combine("Styles", "SurfaceVariants.axaml"),
                "controls|AppButton.button-xs", "8,3", "button-xs"),
            (Path.Combine("Styles", "SurfaceVariants.axaml"),
                "controls|AppButton.button-xxs", "8,3", "button-xxs"),
            (Path.Combine("Styles", "SurfaceVariants.axaml"),
                "controls|AppButton.button-sm", "12,5", "button-sm"),
            (Path.Combine("Styles", "SurfaceVariants.axaml"),
                "controls|AppButton.menu-trigger", "6,3", "menu-trigger"),
            (Path.Combine("Styles", "SurfaceVariants.axaml"),
                "controls|AppButton.composer-send", "0", "composer-send"),
            (Path.Combine("Styles", "ActionButtonVariants.axaml"),
                "controls|AppButton.composer-model-picker", "10,5", "composer-model-picker"),
            (Path.Combine("Styles", "ThemeModePickerVariants.axaml"),
                ".app-theme-mode-picker controls|AppButton.theme-mode-button", "0", "theme-mode-button")
        ];

        foreach (var (file, selector, padding, _) in variants)
        {
            var document = XDocument.Load(Path.Combine(appPath, file));
            var style = Assert.Single(document.Descendants(), element =>
                element.Name.LocalName == "Style" && element.Attribute("Selector")?.Value == selector);
            Assert.Contains(style.Elements(), element => element.Name.LocalName == "Setter"
                && element.Attribute("Property")?.Value == "Padding"
                && element.Attribute("Value")?.Value == padding);
        }

        foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, "Views"), "*.axaml",
            SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            Assert.DoesNotContain(document.Descendants(), element => element.Name.LocalName == "AppButton"
                && element.Attribute("Padding") is not null
                && element.Attribute("Classes")?.Value.Split(' ')
                    .Any(value => variants.Any(variant => variant.Variant == value)) == true);
        }
    }

    [Theory]
    [InlineData("align-leading", "Left")]
    [InlineData("align-stretch", "Stretch")]
    public void ButtonAlignmentVariants_OwnContentAlignment(string variant, string alignment)
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var style = Assert.Single(document.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == $"controls|AppButton.{variant}");

        Assert.Contains(style.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "HorizontalContentAlignment"
            && element.Attribute("Value")?.Value == alignment);
    }

    [Fact]
    public void SurfaceVariants_DeclareSharedCardAndFieldStyles()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "SurfaceVariants.axaml"));
        var selectors = document.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .SelectMany(element => (element.Attribute("Selector")?.Value ?? string.Empty)
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .ToArray();

        Assert.Contains("controls|AppCard.flyout-surface", selectors);
        Assert.Contains("controls|AppCard.composer-surface", selectors);
        Assert.Contains(".app-input.terminal-input", selectors);
        Assert.Contains(".app-combo-box.compact-selector", selectors);
        Assert.Contains(".app-scroll-viewer.modal-scroll", selectors);
        Assert.Contains(".app-scroll-viewer.content-gutter", selectors);
        var navigation = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "NavigationActionVariants.axaml"));
        Assert.Contains(navigation.Descendants(), element => element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "Grid.task-navigation-row.selected controls|AppButton.ghost:pointerover");
    }

    [Fact]
    public void SharedTextStyle_AlignsTextWithIcons()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        var textStyle = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "Style"
            && element.Attribute("Selector")?.Value == "controls|AppText");
        var setters = textStyle.Elements()
            .Where(element => element.Name.LocalName == "Setter")
            .ToDictionary(element => element.Attribute("Property")!.Value,
                element => element.Attribute("Value")!.Value);

        Assert.Equal("13", setters["FontSize"]);
        Assert.Equal("Center", setters["VerticalAlignment"]);
    }

    [Fact]
    public void TypographyVariants_DeclareSidebarHierarchy()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "TypographyVariants.axaml"));
        var selectors = document.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToArray();

        Assert.Contains("controls|AppText.sidebar-brand", selectors);
        Assert.Contains("controls|AppText.sidebar-action", selectors);
        Assert.Contains("controls|AppText.sidebar-row", selectors);
    }

    [Fact]
    public void SettingsPages_UseSharedHeadingTypography()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var variants = XDocument.Load(Path.Combine(appPath, "Styles", "TypographyVariants.axaml"));
        var selectors = variants.Descendants()
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToArray();

        Assert.Contains("controls|AppText.settings-title", selectors);
        Assert.Contains("controls|AppText.settings-description", selectors);
        Assert.Contains("controls|AppText.settings-section-title", selectors);
        var titleStyle = Assert.Single(variants.Descendants(), element =>
            element.Attribute("Selector")?.Value == "controls|AppText.settings-title");
        Assert.Contains(titleStyle.Elements(), element => element.Attribute("Property")?.Value == "FontSize"
            && element.Attribute("Value")?.Value == "28");

        foreach (var page in new[] { "AgentSettingsPane", "AppearanceSettingsPane",
            "BrowserSettingsPane", "McpSettingsPane", "ProviderAndModelSettingsPane",
            "ProxySettingsPane", "SubagentSettingsPane" })
        {
            var document = XDocument.Load(Path.Combine(appPath, "Views", $"{page}.axaml"));
            var header = document.Descendants().SingleOrDefault(element => element.Name.LocalName == "AppSettingsPageHeader");
            if (header is not null)
            {
                Assert.NotNull(header.Attribute("Title"));
                Assert.NotNull(header.Attribute("Description"));
            }
            var typography = header is null ? document : XDocument.Load(Path.Combine(appPath, "Controls", "AppSettingsPageHeader.axaml"));
            foreach (var variant in page == "AppearanceSettingsPane"
                ? new[] { "settings-title" }
                : new[] { "settings-title", "settings-description" })
            {
                var headings = typography.Descendants().Where(element =>
                    element.Name.LocalName == "AppText"
                    && element.Attribute("Classes")?.Value.Split(' ').Contains(variant) == true)
                    .ToArray();
                Assert.NotEmpty(headings);
                foreach (var heading in headings)
                {
                    Assert.Null(heading.Attribute("FontSize"));
                    Assert.Null(heading.Attribute("FontWeight"));
                    Assert.Null(heading.Attribute("Foreground"));
                }
            }
        }
    }

    [Fact]
    public void ProviderModelSettings_UseSharedChevronAndNamedFields()
    {
        var document = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "ProviderAndModelSettingsPane.axaml"));
        var header = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppProviderSettingsHeader");
        Assert.Equal("{Binding IsExpanded}", header.Attribute("IsExpanded")?.Value);
        var headerDocument = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Controls", "AppProviderSettingsHeader.axaml"));
        var expansionButton = Assert.Single(headerDocument.Descendants(), element =>
            element.Name.LocalName == "AppIconButton"
            && element.Attribute("Label")?.Value == "{Binding ExpansionLabel, ElementName=Header}");

        Assert.Null(expansionButton.Attribute("Content"));
        Assert.Equal("{StaticResource UiIconChevronRight}", expansionButton.Attribute("IconData")?.Value);
        Assert.Equal("{StaticResource UiIconChevronDown}", expansionButton.Attribute("AlternateIconData")?.Value);
        Assert.Equal("{Binding IsExpanded, ElementName=Header}", expansionButton.Attribute("IsAlternateIcon")?.Value);
        foreach (var field in document.Descendants().Where(element =>
            element.Name.LocalName is "AppInput" or "AppComboBox" or "AppNumericUpDown"))
        {
            Assert.False(string.IsNullOrWhiteSpace(field.Attribute("AutomationProperties.Name")?.Value));
        }
    }

    [Fact]
    public void AppearanceSettings_GroupsBasicAndAdvancedThemeFontSelectors()
    {
        var fontCard = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Controls", "AppThemePaletteCard.axaml"));
        var card = Assert.Single(fontCard.Descendants(), element => element.Name.LocalName == "AppCard");
        var extended = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Controls", "AppThemeExtendedFontsCard.axaml"));
        Assert.Contains("card", card.Attribute("Classes")?.Value.Split(' ') ?? []);
        Assert.Contains(card.Descendants(), element => element.Name.LocalName == "AppPillComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding Fonts.SelectedUiFontOption}");
        var appearance = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Views", "AppearanceSettingsPane.axaml"));
        Assert.Single(appearance.Descendants(), element => element.Name.LocalName == "AppThemeExtendedFontsCard"
            && element.Attribute("DataContext")?.Value == "{Binding ActivePalette.Fonts}"
            && element.Ancestors().Any(parent => parent.Name.LocalName == "AppExpander"));
        foreach (var selector in new[]
        {
            "SelectedContentFontOption", "SelectedCodeFontOption"
        })
            Assert.Contains(extended.Descendants(), element => element.Name.LocalName == "AppComboBox"
                && element.Attribute("SelectedItem")?.Value == $"{{Binding {selector}}}");
        var fontSelectors = card.Descendants().Concat(extended.Descendants()).Where(element =>
            element.Name.LocalName is "AppComboBox" or "AppPillComboBox"
            && element.Attribute("SelectedItem")?.Value.Contains("FontOption", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(3, fontSelectors.Length);
        Assert.All(fontSelectors, comboBox =>
        {
            Assert.Null(comboBox.Attribute("HorizontalAlignment"));
            if (comboBox.Name.LocalName == "AppPillComboBox") return;
            Assert.Equal("AppSettingsRow", comboBox.Parent?.Name.LocalName);
            Assert.Contains("font-control", comboBox.Parent?.Attribute("Classes")?.Value.Split(' ') ?? []);
        });
        var pillStyles = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App",
            "Styles", "PillSelectorVariants.axaml"));
        Assert.Contains(pillStyles.Descendants(), element => element.Attribute("Selector")?.Value == ".app-combo-box.pill-selector"
            && element.Elements().Any(setter => setter.Attribute("Property")?.Value == "HorizontalAlignment"
                && setter.Attribute("Value")?.Value == "Right"));
    }

    [Fact]
    public void Cards_UseOnlySharedInsetVariants()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var insets = XDocument.Load(Path.Combine(appPath, "Styles", "CardInsetVariants.axaml"));
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "StyleInclude"
            && element.Attribute("Source")?.Value == "avares://MossAgent.App/Styles/CardInsetVariants.axaml");
        var cardStyles = application.Descendants().Concat(insets.Descendants())
            .Where(element => element.Name.LocalName == "Style"
                && element.Attribute("Selector")?.Value.StartsWith("controls|AppCard", StringComparison.Ordinal) == true)
            .ToDictionary(element => element.Attribute("Selector")!.Value);
        var defaultCard = cardStyles["controls|AppCard"];
        Assert.Contains(defaultCard.Elements(), element => element.Name.LocalName == "Setter"
            && element.Attribute("Property")?.Value == "Padding"
            && element.Attribute("Value")?.Value == "12");

        foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, "Views"), "*.axaml",
            SearchOption.AllDirectories))
        {
            foreach (var card in XDocument.Load(file).Descendants()
                .Where(element => element.Name.LocalName == "AppCard"))
            {
                Assert.Null(card.Attribute("Padding"));
                foreach (var variant in (card.Attribute("Classes")?.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Where(value => value.StartsWith("card-inset-", StringComparison.Ordinal)))
                {
                    Assert.Contains($"controls|AppCard.{variant}", cardStyles.Keys);
                }
            }
        }
    }

    [Fact]
    public void CodeFont_UsesOneSharedResourceAndNoViewLocalFontFamilies()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var application = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        Assert.Contains(application.Descendants(), element => element.Name.LocalName == "FontFamily"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "CodeFontFamily"));

        var typography = XDocument.Load(Path.Combine(appPath, "Styles", "TypographyVariants.axaml"));
        var surface = XDocument.Load(Path.Combine(appPath, "Styles", "SurfaceVariants.axaml"));
        var selectors = typography.Descendants().Concat(surface.Descendants())
            .Where(element => element.Name.LocalName == "Style")
            .Select(element => element.Attribute("Selector")?.Value)
            .ToArray();
        Assert.Contains("controls|AppText.code-font", selectors);
        Assert.Contains("controls|AppSelectableText.code-font", selectors);
        Assert.Contains("controls|SyntaxHighlightedSelectableTextBlock.code-font", selectors);
        Assert.Contains(".app-input.code-font", selectors);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(appPath, "Views"), "*.axaml",
            SearchOption.AllDirectories))
        {
            var document = XDocument.Load(file);
            Assert.DoesNotContain(document.Descendants(), element =>
                element.Attribute("FontFamily") is not null);
        }

        var extendedFonts = XDocument.Load(Path.Combine(appPath, "Controls", "AppThemeExtendedFontsCard.axaml"));
        Assert.Contains(extendedFonts.Descendants(), element => element.Name.LocalName == "AppComboBox"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedCodeFontOption}");
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
