using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppearancePaletteInteractionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task KeyboardDisclosurePreservesEditorsAndTheirState(bool dark) => WithPane(dark, (window, pane, fixture) =>
    {
        var disclosure = pane.GetVisualDescendants().OfType<AppExpander>().Single();
        var toggle = disclosure.GetVisualDescendants().OfType<AppToggleButton>().Single();
        Assert.Equal("高级", AutomationProperties.GetName(toggle));
        var chevron = disclosure.GetVisualDescendants().OfType<AppIcon>().Single();
        var closedIcon = chevron.Data;
        var field = pane.GetVisualDescendants().OfType<AppInput>().First();
        field.Text = "#EFF3F7";
        Assert.False(disclosure.IsExpanded);
        Assert.True(toggle.Focus());
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Flush(window);
        Assert.True(disclosure.IsExpanded);
        Assert.NotSame(closedIcon, chevron.Data);
        var fonts = disclosure.GetVisualDescendants().OfType<AppThemeExtendedFontsCard>().Single();
        Assert.True(fonts.IsEffectivelyVisible);
        var codeFont = fonts.GetVisualDescendants().OfType<AppComboBox>()
            .Single(control => AutomationProperties.GetName(control) == "主题代码字体");
        codeFont.SelectedIndex = 1;
        var selected = codeFont.SelectedItem;
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Flush(window);
        Assert.False(disclosure.IsExpanded);
        Assert.Same(closedIcon, chevron.Data);
        Assert.False(fonts.IsEffectivelyVisible);
        window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, null);
        Flush(window);
        Assert.True(disclosure.IsExpanded);
        Assert.Same(fonts, disclosure.GetVisualDescendants().OfType<AppThemeExtendedFontsCard>().Single());
        Assert.Same(field, pane.GetVisualDescendants().OfType<AppInput>().First());
        Assert.Equal("#EFF3F7", field.Text);
        Assert.Same(selected, codeFont.SelectedItem);
        Assert.Same(selected, fixture.Main.ActivePalette.Fonts.SelectedCodeFontOption);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task DisabledDisclosureRejectsClicksAndRecoversWhenEnabled(bool dark) => WithPane(dark, (window, pane, fixture) =>
    {
        var disclosure = pane.GetVisualDescendants().OfType<AppExpander>().Single();
        var toggle = disclosure.GetVisualDescendants().OfType<AppToggleButton>().Single();
        disclosure.IsEnabled = false;
        Flush(window);
        Assert.False(toggle.Focus());
        Click(window, toggle);
        Assert.False(disclosure.IsExpanded);
        disclosure.IsEnabled = true;
        Click(window, toggle);
        Assert.True(disclosure.IsExpanded);
    });

    [Fact]
    public Task SwitchingThemesRoutesTheSameControlsToRetainedIndependentDrafts() => WithPane(false, (window, pane, fixture) =>
    {
        var palette = pane.GetVisualDescendants().OfType<AppThemePaletteCard>().Single();
        var field = palette.GetVisualDescendants().OfType<AppInput>().First();
        var light = fixture.Main.Palette.Light;
        var dark = fixture.Main.Palette.Dark;
        field.Text = "#EFF3F7";
        fixture.Main.SelectThemeCommand.Execute("Dark");
        Flush(window);
        Assert.Same(dark, palette.DataContext);
        field.Text = "#202020";
        fixture.Main.SelectThemeCommand.Execute("Light");
        Flush(window);
        Assert.Same(light, palette.DataContext);
        Assert.Equal("#EFF3F7", field.Text);
        fixture.Main.SelectThemeCommand.Execute("Dark");
        Flush(window);
        Assert.Equal("#202020", field.Text);
        Assert.Same(field, palette.GetVisualDescendants().OfType<AppInput>().First());
        Assert.Equal("#EFF3F7", light.BackgroundHex);
        Assert.Equal("#202020", dark.BackgroundHex);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task SystemPreferenceUpdatesTheActiveEditorWhenTheEffectiveThemeChanges(bool dark) => WithPane(false, (window, pane, fixture) =>
    {
        fixture.Main.SelectThemeCommand.Execute("System");
        Flush(window);
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        Flush(window);
        Assert.True(fixture.Main.IsSystemThemeSelected);
        var expected = dark ? fixture.Main.Palette.Dark : fixture.Main.Palette.Light;
        Assert.Same(expected, fixture.Main.ActivePalette);
        Assert.Same(expected, pane.GetVisualDescendants().OfType<AppThemePaletteCard>().Single().DataContext);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task BasicSaveRejectsUnreadableColorsAndRecoversThroughTheRealButton(bool dark) => WithPane(dark, (window, pane, fixture) =>
    {
        var editor = fixture.Main.ActivePalette;
        var other = dark ? fixture.Main.Palette.Light : fixture.Main.Palette.Dark;
        var otherBackground = other.BackgroundHex;
        var fields = pane.GetVisualDescendants().OfType<AppInput>().ToArray();
        fields[0].Text = "#FFFFFF";
        fields[1].Text = "#FFFFFF";
        var save = pane.GetVisualDescendants().OfType<AppIconButton>().Single(button => button.Label == "应用颜色");
        Click(window, save);
        Assert.True(editor.HasError);
        Assert.Equal("#FFFFFF", fields[0].Text);
        fields[0].Text = dark ? "#202020" : "#EFF3F7";
        fields[1].Text = dark ? "#F0F0F0" : "#202A31";
        Click(window, save);
        Assert.False(editor.HasError);
        Assert.True(editor.HasStatus);
        Assert.Equal(otherBackground, other.BackgroundHex);
    });

    private Task WithPane(bool dark, Action<AppWindow, AppearanceSettingsPane, WorkspaceModelSelectionFixture> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SelectThemeCommand.Execute(dark ? "Dark" : "Light");
        var pane = new AppearanceSettingsPane { DataContext = fixture.Main };
        var window = new AppWindow { Width = 728, Height = 1000, Content = pane };
        window.Show();
        try
        {
            Flush(window);
            action(window, pane, fixture);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Click(AppWindow window, Control control)
    {
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(center, MouseButton.Left);
        window.MouseUp(center, MouseButton.Left);
        Flush(window);
    }

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
