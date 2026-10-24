using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

public sealed class ThemeFontEditorViewModel : ObservableObject
{
    private static readonly IReadOnlyList<UiFontOption> UiOptions =
    [
        new(UiFontPreference.Default, SettingsText.Get("UiFontDefault")),
        new(UiFontPreference.SegoeUi, "Segoe UI"),
        new(UiFontPreference.Inter, "Inter")
    ];

    private static readonly IReadOnlyList<ContentFontOption> ContentOptions =
    [
        new(ContentFontPreference.SameAsUi, UiText.T("与界面字体相同")),
        new(ContentFontPreference.Inter, "Inter"),
        new(ContentFontPreference.SegoeUi, "Segoe UI")
    ];

    private static readonly IReadOnlyList<CodeFontOption> CodeOptions =
    [
        new(CodeFontPreference.System, UiText.T("系统默认")),
        new(CodeFontPreference.Consolas, "Consolas"),
        new(CodeFontPreference.CascadiaCode, "Cascadia Code")
    ];

    private readonly ThemePreference _theme;
    private readonly IUiFontService _uiFonts;
    private readonly IContentFontService _contentFonts;
    private readonly ICodeFontService _codeFonts;
    private string? _error;
    private string? _status;

    public ThemeFontEditorViewModel(ThemePreference theme, IUiFontService uiFonts,
        IContentFontService contentFonts, ICodeFontService codeFonts)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));
        _theme = theme;
        _uiFonts = uiFonts;
        _contentFonts = contentFonts;
        _codeFonts = codeFonts;
    }

    public IReadOnlyList<UiFontOption> UiFontOptions => UiOptions;

    public IReadOnlyList<ContentFontOption> ContentFontOptions => ContentOptions;

    public IReadOnlyList<CodeFontOption> CodeFontOptions => CodeOptions;

    public UiFontOption SelectedUiFontOption
    {
        get => UiOptions.First(option => option.Value == _uiFonts.PreferenceFor(_theme));
        set
        {
            if (value is null || value.Value == _uiFonts.PreferenceFor(_theme)) return;
            _uiFonts.SetPreference(_theme, value.Value);
            OnPropertyChanged();
            SetFeedback(_uiFonts.PersistenceError, UiText.T("界面字体已应用。"));
        }
    }

    public ContentFontOption SelectedContentFontOption
    {
        get => ContentOptions.First(option => option.Value == _contentFonts.PreferenceFor(_theme));
        set
        {
            if (value is null || value.Value == _contentFonts.PreferenceFor(_theme)) return;
            _contentFonts.SetPreference(_theme, value.Value);
            OnPropertyChanged();
            SetFeedback(_contentFonts.PersistenceError, UiText.T("内容字体已应用。"));
        }
    }

    public CodeFontOption SelectedCodeFontOption
    {
        get => CodeOptions.First(option => option.Value == _codeFonts.PreferenceFor(_theme));
        set
        {
            if (value is null || value.Value == _codeFonts.PreferenceFor(_theme)) return;
            _codeFonts.SetPreference(_theme, value.Value);
            OnPropertyChanged();
            SetFeedback(_codeFonts.PersistenceError, UiText.T("代码字体已应用。"));
        }
    }

    public string? Error
    {
        get => _error;
        private set
        {
            if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(Error);

    public string? Status
    {
        get => _status;
        private set
        {
            if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus));
        }
    }

    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);

    private void SetFeedback(string? error, string success)
    {
        Error = error;
        Status = error is null ? success : null;
    }
}

