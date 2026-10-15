using System.Xml.Linq;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class DefaultUiFontFidelityTests
{
    [Theory]
    [InlineData(false, UiFontPreference.Default, "Segoe UI")]
    [InlineData(true, UiFontPreference.Default, "Segoe UI")]
    [InlineData(false, UiFontPreference.SegoeUi, "Segoe UI")]
    [InlineData(true, UiFontPreference.SegoeUi, "Segoe UI")]
    [InlineData(false, UiFontPreference.Inter, "fonts:Inter#Inter")]
    [InlineData(true, UiFontPreference.Inter, "fonts:Inter#Inter")]
    public void ActiveUiAndUncustomizedContentResolveChosenFont(bool dark, UiFontPreference preference, string family)
    {
        using var theme = new ThemeService();
        theme.SetPreference(dark ? ThemePreference.Dark : ThemePreference.Light);
        using var uiFonts = new UiFontService(theme);
        using var contentFonts = new ContentFontService(theme, uiFonts);
        var active = dark ? ThemePreference.Dark : ThemePreference.Light;

        uiFonts.SetPreference(active, preference);

        Assert.Equal(preference, uiFonts.PreferenceFor(active));
        Assert.Equal(family, uiFonts.FamilyName);
        Assert.Equal(family, contentFonts.FamilyName);
        Assert.Equal(ContentFontPreference.SameAsUi, contentFonts.PreferenceFor(active));
    }

    [Fact]
    public void StartupSharedResourcesUseWindowsSystemFont()
    {
        var markup = XDocument.Load(Path.Combine(RepositoryRoot(), "src", "MossAgent.App", "App.axaml"));
        foreach (var name in new[] { "UiFontFamily", "ContentFontFamily" })
        {
            var resource = Assert.Single(markup.Descendants(), element => element.Name.LocalName == "FontFamily"
                && element.Attributes().Any(attribute => attribute.Name.LocalName == "Key" && attribute.Value == name));
            Assert.Equal("Segoe UI", resource.Value);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EditorOffersSystemDefaultAndExplicitInter(bool dark)
    {
        using var theme = new ThemeService();
        theme.SetPreference(dark ? ThemePreference.Dark : ThemePreference.Light);
        using var uiFonts = new UiFontService(theme);
        using var contentFonts = new ContentFontService(theme, uiFonts);
        using var codeFonts = new CodeFontService(theme);
        var active = dark ? ThemePreference.Dark : ThemePreference.Light;
        var editor = new ThemeFontEditorViewModel(active, uiFonts, contentFonts, codeFonts);

        Assert.Equal("系统默认", editor.SelectedUiFontOption.Label);
        Assert.Single(editor.UiFontOptions, option => option.Value == UiFontPreference.SegoeUi);
        var inter = Assert.Single(editor.UiFontOptions, option => option.Value == UiFontPreference.Inter);
        Assert.Equal("Inter", inter.Label);
        editor.SelectedUiFontOption = inter;

        Assert.Equal(UiFontPreference.Inter, uiFonts.PreferenceFor(active));
        Assert.Equal("fonts:Inter#Inter", uiFonts.FamilyName);
        Assert.Equal("fonts:Inter#Inter", contentFonts.FamilyName);
    }

    [Fact]
    public void ExplicitInterPersistsIndependentlyWithoutReplacingOtherSettings() => WithStore((store, path) =>
    {
        using var theme = new ThemeService(store);
        using (var uiFonts = new UiFontService(theme, store))
        {
            uiFonts.SetPreference(ThemePreference.Light, UiFontPreference.Inter);
            uiFonts.SetPreference(ThemePreference.Dark, UiFontPreference.SegoeUi);
        }
        using var reloaded = new UiFontService(theme, store);
        Assert.Equal(UiFontPreference.Inter, reloaded.PreferenceFor(ThemePreference.Light));
        Assert.Equal(UiFontPreference.SegoeUi, reloaded.PreferenceFor(ThemePreference.Dark));
        theme.SetPreference(ThemePreference.Light);
        Assert.Equal("fonts:Inter#Inter", reloaded.FamilyName);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(42, document.RootElement.GetProperty("Other").GetInt32());
        Assert.Equal("Consolas", document.RootElement.GetProperty("CodeFont").GetString());
    });

    [Fact]
    public void DefaultLoadAndUndefinedValueDoNotRewriteExistingSettings() => WithStore((store, path) =>
    {
        var original = File.ReadAllText(path);
        using var theme = new ThemeService(store);
        using var uiFonts = new UiFontService(theme, store);

        Assert.Equal(0, (int)UiFontPreference.Default);
        Assert.Equal(1, (int)UiFontPreference.SegoeUi);
        Assert.Equal("Segoe UI", uiFonts.FamilyName);
        uiFonts.SetPreference(ThemePreference.Light, (UiFontPreference)99);
        Assert.Equal(UiFontPreference.Default, uiFonts.PreferenceFor(ThemePreference.Light));
        Assert.Equal(original, File.ReadAllText(path));
    });

    private static void WithStore(Action<JsonAppearancePreferenceStore, string> verify)
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-default-font-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"Theme":"Light","UiFont":"Default","CodeFont":"Consolas","Other":42}""");
            verify(new JsonAppearancePreferenceStore(new AppDataPaths(root)), path);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found");
    }
}
