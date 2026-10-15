using MossAgent.Application.Settings;

namespace MossAgent.App.Services;

public interface IContentFontService
{
    ContentFontPreference PreferenceFor(ThemePreference theme);

    string FamilyName { get; }

    string? PersistenceError { get; }

    void SetPreference(ThemePreference theme, ContentFontPreference preference);
}
