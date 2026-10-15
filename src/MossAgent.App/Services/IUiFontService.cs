using MossAgent.Application.Settings;

namespace MossAgent.App.Services;

public interface IUiFontService
{
    UiFontPreference PreferenceFor(ThemePreference theme);

    string FamilyName { get; }

    string? PersistenceError { get; }

    event Action? Changed;

    void SetPreference(ThemePreference theme, UiFontPreference preference);
}
