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
public sealed class AppearanceModeLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 384)]
    [InlineData(true, 384)]
    [InlineData(false, 728)]
    [InlineData(true, 728)]
    public Task ModeChoicesUseCompactEqualHitAreasInOneRow(bool dark, double width) => _session.Dispatch(() =>
    {
        Avalonia.Application.Current!.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        var pane = new AppearanceSettingsPane();
        var window = new AppWindow { Width = width, Height = 900, Content = pane };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var choices = pane.GetVisualDescendants().OfType<AppButton>()
                .Where(button => AutomationProperties.GetName(button) is "系统主题" or "浅色主题" or "深色主题").ToArray();
            Assert.Equal(3, choices.Length);
            Assert.All(choices, button => Assert.Equal(new Size(80, 60), button.Bounds.Size));
            var origins = choices.Select(button => button.TranslatePoint(default, pane)!.Value).ToArray();
            Assert.All(origins, origin => Assert.Equal(origins[0].Y, origin.Y));
            Assert.Equal(96, origins[1].X - origins[0].X);
            Assert.Equal(96, origins[2].X - origins[1].X);
            Assert.All(origins, origin => Assert.InRange(origin.X, 0, width - 80));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
    [Theory]
    [InlineData("System")]
    [InlineData("Light")]
    [InlineData("Dark")]
    public Task KeyboardSelectionKeepsTheProductionThemeCommandAndSelectedState(string mode) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.SelectThemeCommand.Execute(mode == "Light" ? "Dark" : "Light");
        var picker = new AppThemeModePicker { DataContext = fixture.Main };
        var window = new AppWindow { Width = 384, Height = 160, Content = picker };
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            var choices = picker.GetVisualDescendants().OfType<AppButton>().ToArray();
            var button = Assert.Single(choices, choice => Equals(choice.CommandParameter, mode));
            Assert.Same(fixture.Main.SelectThemeCommand, button.Command);
            Assert.True(button.Focus());
            window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, null);
            Dispatcher.UIThread.RunJobs();
            Assert.Same(button, Assert.Single(choices, choice => choice.Classes.Contains("selected")));
            Assert.Equal(mode == "System", fixture.Main.IsSystemThemeSelected);
            Assert.Equal(mode == "Light", fixture.Main.IsLightThemeSelected);
            Assert.Equal(mode == "Dark", fixture.Main.IsDarkThemeSelected);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
