using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.Application.Settings;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ThemePresetChoiceTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DefaultChoiceDoesNotWritePreferencesOrPretendToOfferCustomThemes(bool dark) => WithCard(dark, fixture =>
    {
        var path = Path.Combine(fixture.Root, "ui-settings.json");
        var original = File.ReadAllText(path);
        var option = Assert.Single(fixture.Editor.ThemeOptions);
        Assert.Equal("默认", option.Label);
        Assert.Same(option, fixture.Editor.SelectedThemeOption);
        fixture.Editor.SelectedThemeOption = option;
        fixture.Editor.SelectedThemeOption = null;
        Assert.Equal(original, File.ReadAllText(path));
        Assert.False(fixture.Editor.HasStatus);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DefaultAndRetainedCustomChoicesPersistOnlyTheirOwnPalette(bool dark) => WithCard(dark, fixture =>
    {
        SaveCustom(fixture);
        var custom = fixture.Editor.SelectedThemeOption!;
        Assert.Equal("自定义", custom.Label);
        Assert.Equal(2, fixture.Editor.ThemeOptions.Count);
        fixture.Editor.Fonts.SelectedUiFontOption = fixture.Editor.Fonts.UiFontOptions[2];
        fixture.Editor.SelectedThemeOption = fixture.Editor.ThemeOptions[0];
        Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme));
        Assert.Equal(UiFontPreference.Inter, fixture.Store.LoadUiFont(fixture.Theme));
        Assert.Same(custom, fixture.Editor.ThemeOptions[1]);
        fixture.Editor.SelectedThemeOption = custom;
        Assert.Equal(custom.Palette, fixture.Store.LoadThemePalette(fixture.Theme));
        Assert.Equal("#2563EB", fixture.Editor.AccentHex);
        Assert.Equal(custom.Palette!.Background, fixture.Editor.BackgroundHex);
        Assert.False(fixture.Editor.HasError);
        var other = dark ? ThemePreference.Light : ThemePreference.Dark;
        Assert.Null(fixture.Store.LoadThemePalette(other));
        Assert.Null(fixture.Store.LoadUiFont(other));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task AccentChoiceRefreshesThePresetWithoutDiscardingUnsavedColorDrafts(bool dark) => WithCard(dark, fixture =>
    {
        fixture.Editor.BackgroundHex = "not-a-color";
        fixture.Editor.ForegroundHex = "#FFFFFF";
        fixture.Editor.SelectedAccentOption = fixture.Editor.AccentOptions[1];
        Assert.Equal("not-a-color", fixture.Editor.BackgroundHex);
        Assert.Equal("#FFFFFF", fixture.Editor.ForegroundHex);
        Assert.Equal("自定义", fixture.Editor.SelectedThemeOption?.Label);
        Assert.Equal("#2563EB", fixture.Editor.SelectedThemeOption?.Palette?.AccentHex);
        var original = File.ReadAllText(Path.Combine(fixture.Root, "ui-settings.json"));
        fixture.Editor.SaveCommand.Execute(null);
        Assert.True(fixture.Editor.HasError);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.Root, "ui-settings.json")));
        fixture.Editor.ResetCommand.Execute(null);
        Assert.Equal("默认", fixture.Editor.SelectedThemeOption?.Label);
        Assert.False(fixture.Editor.HasError);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task UnknownOrForeignChoicesCannotChangeTheServiceOrDiscardDrafts(bool dark) => WithCard(dark, fixture =>
    {
        SaveCustom(fixture);
        var selected = fixture.Editor.SelectedThemeOption;
        var original = File.ReadAllText(Path.Combine(fixture.Root, "ui-settings.json"));
        fixture.Editor.BackgroundHex = "draft";
        fixture.Editor.SelectedThemeOption = new ThemePaletteOption(null, "默认");
        fixture.Editor.SelectedThemeOption = new ThemePaletteOption(ThemePalette.DarkDefault, "自定义");
        Assert.Same(selected, fixture.Editor.SelectedThemeOption);
        Assert.Equal("draft", fixture.Editor.BackgroundHex);
        Assert.Equal(original, File.ReadAllText(Path.Combine(fixture.Root, "ui-settings.json")));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailedPresetSaveKeepsOpaquePreferencesAndReportsOnlyAnError(bool dark) => WithCard(dark, fixture =>
    {
        SaveCustom(fixture);
        var path = Path.Combine(fixture.Root, "ui-settings.json");
        const string original = "{\"ThemePalettes\":\"opaque\",\"Other\":42}";
        File.WriteAllText(path, original);
        fixture.Editor.SelectedThemeOption = fixture.Editor.ThemeOptions[0];
        Assert.Equal(original, File.ReadAllText(path));
        Assert.True(fixture.Editor.HasError);
        Assert.False(fixture.Editor.HasStatus);
        Assert.Equal("默认", fixture.Editor.SelectedThemeOption?.Label);
        fixture.Editor.SelectedThemeOption = fixture.Editor.ThemeOptions[1];
        Assert.Equal(original, File.ReadAllText(path));
        Assert.True(fixture.Editor.HasError);
        Assert.False(fixture.Editor.HasStatus);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReopenedEditorRecoversTheCurrentCustomPaletteWithoutWritingIt(bool dark) => WithCard(dark, fixture =>
    {
        SaveCustom(fixture);
        var path = Path.Combine(fixture.Root, "ui-settings.json");
        var original = File.ReadAllText(path);
        using var theme = new ThemeService(fixture.Store);
        using var uiFonts = new UiFontService(theme, fixture.Store);
        using var contentFonts = new ContentFontService(theme, uiFonts, fixture.Store);
        using var codeFonts = new CodeFontService(theme, fixture.Store);
        var settings = new ThemePaletteSettingsViewModel(theme, uiFonts, contentFonts, codeFonts);
        var editor = dark ? settings.Dark : settings.Light;
        Assert.Equal(fixture.Editor.SelectedThemeOption, editor.SelectedThemeOption);
        Assert.Equal(2, editor.ThemeOptions.Count);
        Assert.Equal(original, File.ReadAllText(path));
        Assert.False(editor.HasStatus);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardPopupSwitchesRealChoicesAndDisabledInputCannotApplyThem(bool dark) => WithCard(dark, fixture =>
    {
        SaveCustom(fixture);
        var selector = Assert.Single(fixture.Card.GetVisualDescendants().OfType<AppPillComboBox>(),
            control => AutomationProperties.GetName(control) == "主题配色");
        Assert.True(selector.Focus(NavigationMethod.Tab));
        Press(fixture, Key.F4);
        Assert.True(selector.IsDropDownOpen);
        Press(fixture, Key.Up);
        Press(fixture, Key.Enter);
        Assert.False(selector.IsDropDownOpen);
        Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme));
        Press(fixture, Key.Down);
        Assert.Equal("#2563EB", fixture.Store.LoadThemePalette(fixture.Theme)?.AccentHex);
        selector.IsEnabled = false;
        Assert.False(selector.Focus(NavigationMethod.Tab));
        Press(fixture, Key.Up);
        Assert.Equal("#2563EB", fixture.Store.LoadThemePalette(fixture.Theme)?.AccentHex);
        selector.IsEnabled = true;
        Assert.True(selector.Focus(NavigationMethod.Tab));
        Press(fixture, Key.Up);
        Assert.Null(fixture.Store.LoadThemePalette(fixture.Theme));
    });

    private Task WithCard(bool dark, Action<ThemePaletteChoiceFixture> action) => _session.Dispatch(() =>
    {
        using var fixture = new ThemePaletteChoiceFixture(dark);
        action(fixture);
    }, TestContext.Current.CancellationToken);

    private static void SaveCustom(ThemePaletteChoiceFixture fixture)
    {
        fixture.Editor.BackgroundHex = fixture.Theme == ThemePreference.Dark ? "#202020" : "#EFF3F7";
        fixture.Editor.ForegroundHex = fixture.Theme == ThemePreference.Dark ? "#F0F0F0" : "#202A31";
        fixture.Editor.SaveCommand.Execute(null);
        fixture.Editor.SelectedAccentOption = fixture.Editor.AccentOptions[1];
        fixture.Flush();
    }

    private static void Press(ThemePaletteChoiceFixture fixture, Key key)
    {
        fixture.Window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, null);
        fixture.Flush();
    }
}
