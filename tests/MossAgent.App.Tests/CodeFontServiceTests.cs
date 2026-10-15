using System.Text.Json;
using MossAgent.App.Services;
using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class CodeFontServiceTests
{
    [Fact]
    public void BothThemesDefaultToSystemMonospace()
    {
        using var theme = new ThemeService();
        using var service = new CodeFontService(theme);

        Assert.Equal(CodeFontPreference.System, service.PreferenceFor(ThemePreference.Light));
        Assert.Equal(CodeFontPreference.System, service.PreferenceFor(ThemePreference.Dark));
        Assert.StartsWith("Consolas", service.FamilyName);
    }

    [Fact]
    public void LegacyCodeFontIsInheritedUntilEachThemeIsCustomized()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"Theme":"Dark","CodeFont":"Consolas","Other":42}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            using var theme = new ThemeService(store);
            using var service = new CodeFontService(theme, store);
            Assert.Equal(CodeFontPreference.Consolas, service.PreferenceFor(ThemePreference.Light));
            Assert.Equal(CodeFontPreference.Consolas, service.PreferenceFor(ThemePreference.Dark));

            service.SetPreference(ThemePreference.Light, CodeFontPreference.CascadiaCode);

            Assert.Equal("Consolas", service.FamilyName);
            theme.SetPreference(ThemePreference.Light);
            Assert.StartsWith("Cascadia Code", service.FamilyName);
            Assert.Equal(CodeFontPreference.CascadiaCode, store.LoadCodeFont(ThemePreference.Light));
            Assert.Null(store.LoadCodeFont(ThemePreference.Dark));
            Assert.Equal(CodeFontPreference.Consolas, store.LoadCodeFont());
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(42, document.RootElement.GetProperty("Other").GetInt32());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ThemeCodeFontsPersistIndependentlyAndReload()
    {
        var root = NewRoot();
        try
        {
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            using var theme = new ThemeService(store);
            using (var service = new CodeFontService(theme, store))
            {
                service.SetPreference(ThemePreference.Light, CodeFontPreference.Consolas);
                service.SetPreference(ThemePreference.Dark, CodeFontPreference.CascadiaCode);
                Assert.Null(service.PersistenceError);
            }

            using var reloaded = new CodeFontService(theme, store);
            Assert.Equal(CodeFontPreference.Consolas, reloaded.PreferenceFor(ThemePreference.Light));
            Assert.Equal(CodeFontPreference.CascadiaCode, reloaded.PreferenceFor(ThemePreference.Dark));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MalformedThemeCodeFontsStayIntactAndShowFailure()
    {
        var root = NewRoot();
        try
        {
            var path = Path.Combine(root, "ui-settings.json");
            File.WriteAllText(path, """{"ThemeCodeFonts":"opaque"}""");
            var store = new JsonAppearancePreferenceStore(new AppDataPaths(root));
            using var theme = new ThemeService(store);
            using var service = new CodeFontService(theme, store);

            service.SetPreference(ThemePreference.Light, CodeFontPreference.CascadiaCode);

            Assert.Equal(CodeFontPreference.CascadiaCode, service.PreferenceFor(ThemePreference.Light));
            Assert.NotNull(service.PersistenceError);
            Assert.Equal("""{"ThemeCodeFonts":"opaque"}""", File.ReadAllText(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void InvalidThemeOrPreferenceCannotChangeCodeFont()
    {
        using var theme = new ThemeService();
        using var service = new CodeFontService(theme);

        service.SetPreference(ThemePreference.Light, (CodeFontPreference)99);

        Assert.Equal(CodeFontPreference.System, service.PreferenceFor(ThemePreference.Light));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.SetPreference(ThemePreference.System, CodeFontPreference.Consolas));
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "mossagent-code-font-test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }
}
