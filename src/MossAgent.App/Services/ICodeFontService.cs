using MossAgent.Application.Settings;

namespace MossAgent.App.Services;

public interface ICodeFontService
{
    CodeFontPreference PreferenceFor(ThemePreference theme);

    string FamilyName { get; }

    string? PersistenceError { get; }

    void SetPreference(ThemePreference theme, CodeFontPreference preference);
}
