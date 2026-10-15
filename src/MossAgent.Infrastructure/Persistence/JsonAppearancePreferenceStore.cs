using System.Text.Json;
using System.Text.Json.Nodes;
using MossAgent.Application.Settings;

namespace MossAgent.Infrastructure.Persistence;

public sealed class JsonAppearancePreferenceStore(AppDataPaths paths) : IAppearancePreferenceStore
{
    private readonly object _gate = new();
    private readonly string _path = Path.Combine(paths.Root, "ui-settings.json");

    public ThemePreference LoadTheme() => Load("Theme", ThemePreference.System);

    public CodeFontPreference LoadCodeFont() => Load("CodeFont", CodeFontPreference.System);

    public UiFontPreference LoadUiFont() => Load("UiFont", UiFontPreference.Default);

    public UiFontPreference? LoadUiFont(ThemePreference theme) =>
        LoadThemeFont<UiFontPreference>("ThemeUiFonts", theme);

    public CodeFontPreference? LoadCodeFont(ThemePreference theme) =>
        LoadThemeFont<CodeFontPreference>("ThemeCodeFonts", theme);

    public ContentFontPreference? LoadContentFont(ThemePreference theme) =>
        LoadThemeFont<ContentFontPreference>("ThemeContentFonts", theme);

    public void SaveTheme(ThemePreference preference)
    {
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Save("Theme", preference.ToString());
    }

    public ThemePalette? LoadThemePalette(ThemePreference theme)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        try
        {
            if (!File.Exists(_path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("ThemePalettes", out var palettes)
                || palettes.ValueKind != JsonValueKind.Object
                || !palettes.TryGetProperty(theme.ToString(), out var saved)
                || saved.ValueKind != JsonValueKind.Object
                || !saved.TryGetProperty("Background", out var background)
                || !saved.TryGetProperty("Foreground", out var foreground)
                || background.ValueKind != JsonValueKind.String
                || foreground.ValueKind != JsonValueKind.String)
                return null;
            var hasAccent = saved.TryGetProperty("Accent", out var accent);
            if (hasAccent && accent.ValueKind != JsonValueKind.String) return null;
            var accentHex = hasAccent ? accent.GetString() : null;
            return ThemePalette.TryCreate(background.GetString(), foreground.GetString(), accentHex,
                out var palette)
                ? palette : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    public void SaveThemePalette(ThemePreference theme, ThemePalette? palette)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        Update(settings =>
        {
            var palettes = settings["ThemePalettes"] as JsonObject;
            if (settings["ThemePalettes"] is not null && palettes is null)
                throw new InvalidDataException("主题颜色设置格式无效。");
            if (palette is null)
            {
                palettes?.Remove(theme.ToString());
                if (palettes?.Count == 0) settings.Remove("ThemePalettes");
                return;
            }

            if (palettes is null)
            {
                palettes = new JsonObject();
                settings["ThemePalettes"] = palettes;
            }
            var savedPalette = new JsonObject
            {
                ["Background"] = palette.Background,
                ["Foreground"] = palette.Foreground
            };
            if (palette.AccentHex is not null) savedPalette["Accent"] = palette.AccentHex;
            palettes[theme.ToString()] = savedPalette;
        });
    }

    public void SaveCodeFont(CodeFontPreference preference)
    {
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Save("CodeFont", preference.ToString());
    }

    public void SaveUiFont(UiFontPreference preference)
    {
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Save("UiFont", preference.ToString());
    }

    public void SaveUiFont(ThemePreference theme, UiFontPreference preference) =>
        SaveThemeFont("ThemeUiFonts", theme, preference);

    public void SaveCodeFont(ThemePreference theme, CodeFontPreference preference) =>
        SaveThemeFont("ThemeCodeFonts", theme, preference);

    public void SaveContentFont(ThemePreference theme, ContentFontPreference preference) =>
        SaveThemeFont("ThemeContentFonts", theme, preference);

    private TPreference? LoadThemeFont<TPreference>(string key, ThemePreference theme)
        where TPreference : struct, Enum
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        try
        {
            if (!File.Exists(_path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(key, out var fonts)
                || fonts.ValueKind != JsonValueKind.Object
                || !fonts.TryGetProperty(theme.ToString(), out var saved)
                || saved.ValueKind != JsonValueKind.String)
                return null;
            return Enum.TryParse<TPreference>(saved.GetString(), true, out var preference)
                && Enum.IsDefined(preference) ? preference : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private void SaveThemeFont<TPreference>(string key, ThemePreference theme, TPreference preference)
        where TPreference : struct, Enum
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        if (!Enum.IsDefined(preference)) throw new ArgumentOutOfRangeException(nameof(preference));
        Update(settings =>
        {
            var fonts = settings[key] as JsonObject;
            if (settings[key] is not null && fonts is null)
                throw new InvalidDataException("主题字体设置格式无效。");
            if (fonts is null)
            {
                fonts = new JsonObject();
                settings[key] = fonts;
            }
            fonts[theme.ToString()] = preference.ToString();
        });
    }

    private TPreference Load<TPreference>(string key, TPreference fallback)
        where TPreference : struct, Enum
    {
        try
        {
            if (!File.Exists(_path)) return fallback;
            using var document = JsonDocument.Parse(File.ReadAllText(_path));
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(key, out var value)
                || value.ValueKind != JsonValueKind.String)
            {
                return fallback;
            }

            return Enum.TryParse<TPreference>(value.GetString(), true, out var preference)
                && Enum.IsDefined(preference) ? preference : fallback;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return fallback;
        }
    }

    private void Save(string key, string value) => Update(settings => settings[key] = value);

    private void Update(Action<JsonObject> update)
    {
        lock (_gate)
        {
            var directory = Path.GetDirectoryName(_path)!;
            Directory.CreateDirectory(directory);
            var settings = File.Exists(_path)
                ? JsonNode.Parse(File.ReadAllText(_path)) as JsonObject
                    ?? throw new InvalidDataException("界面设置文件格式无效。")
                : new JsonObject();
            update(settings);

            var temporary = Path.Combine(directory, $".ui-settings-{Guid.NewGuid():N}.tmp");
            try
            {
                File.WriteAllText(temporary, settings.ToJsonString());
                File.Move(temporary, _path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
    }
}
