using System.Text.Json;
using System.Xml.Linq;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ThemeContentFontSettingsTests
{
    [Fact]
    public void DefaultContentFontFollowsActiveUiFontUntilCustomized()
    {
        using var theme = new ThemeService();
        using var uiFonts = new UiFontService(theme);
        using var contentFonts = new ContentFontService(theme, uiFonts);

        uiFonts.SetPreference(ThemePreference.Light, UiFontPreference.SegoeUi);
        Assert.Equal("Segoe UI", contentFonts.FamilyName);

        contentFonts.SetPreference(ThemePreference.Light, ContentFontPreference.Inter);
        Assert.Equal("fonts:Inter#Inter", contentFonts.FamilyName);
        theme.SetPreference(ThemePreference.Dark);
        Assert.Equal(ContentFontPreference.SameAsUi, contentFonts.PreferenceFor(ThemePreference.Dark));
        Assert.Equal("Segoe UI", contentFonts.FamilyName);
    }

    [Fact]
    public void ThemeContentFontsPersistSeparatelyWithoutReplacingOtherSettings()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"Theme":"Dark","CodeFont":"Consolas","Other":42}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            using var theme = new ThemeService(store);
            using var uiFonts = new UiFontService(theme, store);
            using var contentFonts = new ContentFontService(theme, uiFonts, store);

            contentFonts.SetPreference(ThemePreference.Light, ContentFontPreference.Inter);
            contentFonts.SetPreference(ThemePreference.Dark, ContentFontPreference.SegoeUi);

            Assert.Equal(ContentFontPreference.Inter, store.LoadContentFont(ThemePreference.Light));
            Assert.Equal(ContentFontPreference.SegoeUi, store.LoadContentFont(ThemePreference.Dark));
            Assert.Equal("Segoe UI", contentFonts.FamilyName);
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("Consolas", document.RootElement.GetProperty("CodeFont").GetString());
            Assert.Equal(42, document.RootElement.GetProperty("Other").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedThemeContentFontsStayIntactAndShowFailure()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"ThemeContentFonts":"opaque"}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            using var theme = new ThemeService(store);
            using var uiFonts = new UiFontService(theme, store);
            using var contentFonts = new ContentFontService(theme, uiFonts, store);
            using var codeFonts = new CodeFontService(theme, store);
            var editor = new ThemeFontEditorViewModel(ThemePreference.Light, uiFonts,
                contentFonts, codeFonts);

            editor.SelectedContentFontOption = Assert.Single(editor.ContentFontOptions,
                option => option.Value == ContentFontPreference.SegoeUi);

            Assert.True(editor.HasError);
            Assert.False(editor.HasStatus);
            Assert.Equal("""{"ThemeContentFonts":"opaque"}""", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidThemeOrPreferenceCannotChangeContentFont()
    {
        using var theme = new ThemeService();
        using var uiFonts = new UiFontService(theme);
        using var contentFonts = new ContentFontService(theme, uiFonts);

        contentFonts.SetPreference(ThemePreference.Light, (ContentFontPreference)99);

        Assert.Equal(ContentFontPreference.SameAsUi,
            contentFonts.PreferenceFor(ThemePreference.Light));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            contentFonts.SetPreference(ThemePreference.System, ContentFontPreference.Inter));
    }

    [Fact]
    public void ConversationProseUsesSharedContentFontWithoutChangingCodeFonts()
    {
        var appPath = Path.Combine(RepositoryRoot(), "src", "MossAgent.App");
        var app = XDocument.Load(Path.Combine(appPath, "App.axaml"));
        var styles = XDocument.Load(Path.Combine(appPath, "Styles", "TypographyVariants.axaml"));
        var chat = XDocument.Load(Path.Combine(appPath, "Views", "WorkspaceMessageView.axaml"));
        var subagents = XDocument.Load(Path.Combine(appPath, "Views", "SubagentDialogueView.axaml"));
        var response = XDocument.Load(Path.Combine(appPath, "Controls", "AppResponseContent.axaml"));

        Assert.Contains(app.Descendants(), element => element.Name.LocalName == "FontFamily"
            && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key"
                && attribute.Value == "ContentFontFamily"));
        foreach (var selector in new[]
        {
            "controls|AppSelectableText.content-font",
            "controls|MarkdownSelectableTextBlock.content-font"
        })
            Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value == selector);
        Assert.Contains(chat.Descendants(), element => element.Name.LocalName == "AppSelectableText"
            && element.Attribute("Classes")?.Value.Contains("content-font") == true);
        Assert.Contains(response.Descendants(), element => element.Name.LocalName == "MarkdownSelectableTextBlock"
            && element.Attribute("Classes")?.Value.Contains("content-font") == true);
        Assert.Contains(subagents.Descendants(), element => element.Name.LocalName == "AppSelectableText"
            && element.Attribute("Classes.content-font")?.Value == "{Binding !IsTool}");
        Assert.Contains(styles.Descendants(), element => element.Attribute("Selector")?.Value
            == "controls|MarkdownSelectableTextBlock.code-font");
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-content-font-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory.FullName;
    }
}
