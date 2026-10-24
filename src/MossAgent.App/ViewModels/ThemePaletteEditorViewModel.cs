using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Resources;
using MossAgent.App.Services;
using MossAgent.Application.Settings;

namespace MossAgent.App.ViewModels;

public sealed class ThemePaletteEditorViewModel : ObservableObject
{
    private static readonly IReadOnlyList<ThemeAccentOption> PresetAccents =
    [
        new(null, UiText.T("默认")),
        new("#2563EB", UiText.T("蓝色")),
        new("#047857", UiText.T("绿色")),
        new("#7C3AED", UiText.T("紫色")),
        new("#C2410C", UiText.T("橙色"))
    ];

    private readonly IThemeService _service;
    private readonly ThemePreference _theme;
    private string _backgroundHex = string.Empty;
    private string _foregroundHex = string.Empty;
    private IReadOnlyList<ThemeAccentOption> _accentOptions = PresetAccents;
    private ThemeAccentOption? _selectedAccentOption;
    private string? _error;
    private string? _status;
    private bool _isRefreshing;
    private readonly ThemePaletteOption _defaultTheme = new(null, AppearanceText.DefaultTheme);
    private ThemePaletteOption? _customTheme;
    private IReadOnlyList<ThemePaletteOption> _themeOptions = [];
    private ThemePaletteOption? _selectedThemeOption;

    public ThemePaletteEditorViewModel(IThemeService service, ThemePreference theme,
        string themeTitle, ThemeFontEditorViewModel fonts)
    {
        if (theme is not (ThemePreference.Light or ThemePreference.Dark))
            throw new ArgumentOutOfRangeException(nameof(theme));

        _service = service;
        _theme = theme;
        ThemeTitle = themeTitle;
        Fonts = fonts;
        SaveCommand = new RelayCommand(Save);
        ResetCommand = new RelayCommand(Reset);
        RefreshFromPalette();
    }

    public string ThemeTitle { get; }

    public ThemeFontEditorViewModel Fonts { get; }

    public IReadOnlyList<ThemePaletteOption> ThemeOptions
    {
        get => _themeOptions;
        private set => SetProperty(ref _themeOptions, value);
    }

    public ThemePaletteOption? SelectedThemeOption
    {
        get => _selectedThemeOption;
        set
        {
            if (_isRefreshing)
            {
                SetProperty(ref _selectedThemeOption, value);
                return;
            }
            if (value is null || ReferenceEquals(value, _selectedThemeOption)
                || !ThemeOptions.Any(option => ReferenceEquals(option, value))) return;

            _service.SetPalette(_theme, value.Palette);
            RefreshFromPalette();
            SetFeedback(_service.PersistenceError,
                _service.PersistenceError is null ? AppearanceText.ThemeApplied : null);
        }
    }

    public string BackgroundHex
    {
        get => _backgroundHex;
        set => SetProperty(ref _backgroundHex, value ?? string.Empty);
    }

    public string ForegroundHex
    {
        get => _foregroundHex;
        set => SetProperty(ref _foregroundHex, value ?? string.Empty);
    }

    public string AccentHex => _service.PaletteFor(_theme).AccentHex
        ?? (_theme == ThemePreference.Dark ? "#F4F4F6" : "#0D0D0D");

    public IReadOnlyList<ThemeAccentOption> AccentOptions
    {
        get => _accentOptions;
        private set => SetProperty(ref _accentOptions, value);
    }

    public ThemeAccentOption? SelectedAccentOption
    {
        get => _selectedAccentOption;
        set
        {
            if (_isRefreshing)
            {
                SetProperty(ref _selectedAccentOption, value);
                return;
            }
            if (value is null || Equals(_selectedAccentOption, value)) return;

            var current = _service.PaletteFor(_theme);
            if (!ThemePalette.TryCreate(current.Background, current.Foreground, value.Value,
                    out var palette))
            {
                SetFeedback(UiText.T("强调色必须为 #RRGGBB，且与白色文字对比度至少为 4.5:1。"), null);
                return;
            }

            SetProperty(ref _selectedAccentOption, value);
            _service.SetPalette(_theme, palette);
            RefreshThemeOptions();
            OnPropertyChanged(nameof(AccentHex));
            SetFeedback(_service.PersistenceError,
                _service.PersistenceError is null ? UiText.T("强调色已应用。") : null);
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

    public IRelayCommand SaveCommand { get; }

    public IRelayCommand ResetCommand { get; }

    private void Save()
    {
        var accentHex = _service.PaletteFor(_theme).AccentHex;
        if (!ThemePalette.TryCreate(BackgroundHex.Trim(), ForegroundHex.Trim(), accentHex,
                out var palette))
        {
            SetFeedback(UiText.T("请输入 #RRGGBB 颜色，且背景与前景对比度至少为 4.5:1。"), null);
            return;
        }

        _service.SetPalette(_theme, palette);
        RefreshFromPalette();
        SetFeedback(_service.PersistenceError,
            _service.PersistenceError is null ? UiText.T("主题颜色已保存。") : null);
    }

    private void Reset()
    {
        _service.SetPalette(_theme, null);
        RefreshFromPalette();
        SetFeedback(_service.PersistenceError,
            _service.PersistenceError is null ? UiText.T("已恢复默认颜色。") : null);
    }

    private void RefreshFromPalette()
    {
        var palette = _service.PaletteFor(_theme);
        BackgroundHex = palette.Background;
        ForegroundHex = palette.Foreground;
        var selected = PresetAccents.FirstOrDefault(option => option.Value == palette.AccentHex)
            ?? new ThemeAccentOption(palette.AccentHex, UiText.T("自定义 {0}", palette.AccentHex));
        _isRefreshing = true;
        AccentOptions = PresetAccents.Contains(selected) ? PresetAccents : [.. PresetAccents, selected];
        SelectedAccentOption = selected;
        _isRefreshing = false;
        OnPropertyChanged(nameof(AccentHex));
        RefreshThemeOptions();
    }

    private void RefreshThemeOptions()
    {
        var palette = _service.PaletteFor(_theme);
        var defaultPalette = _theme == ThemePreference.Light ? ThemePalette.LightDefault : ThemePalette.DarkDefault;
        var isDefault = palette == defaultPalette;
        if (!isDefault && _customTheme?.Palette != palette)
            _customTheme = new ThemePaletteOption(palette, AppearanceText.CustomTheme);

        _isRefreshing = true;
        try
        {
            if (ThemeOptions.Count == 0 || ThemeOptions.Last() != (_customTheme ?? _defaultTheme))
                ThemeOptions = _customTheme is null ? [_defaultTheme] : [_defaultTheme, _customTheme];
            SelectedThemeOption = isDefault ? _defaultTheme : _customTheme;
        }
        finally { _isRefreshing = false; }
    }

    private void SetFeedback(string? error, string? status)
    {
        Error = error;
        Status = status;
    }
}

