using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationVisualLayoutTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(980, false)]
    [InlineData(1280, false)]
    [InlineData(980, true)]
    [InlineData(1280, true)]
    public Task SelectedCard_HasNoNativeSelectionFrameAndStaysWithinTheList(int width, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        await fixture.Automations.SaveAsync(Schedule(fixture), fixture.Token);
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        await fixture.Main.Pages.Automations!.ReloadAsync();
        var window = Window(fixture, width, 820, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            var list = Assert.Single(window.GetVisualDescendants().OfType<AppListBox>(), control => control.Classes.Contains("automation-list"));
            Assert.Contains("thin-scrollbar", list.Classes);
            var scroll = Assert.Single(list.GetVisualDescendants().OfType<ScrollViewer>(), control => ReferenceEquals(control.TemplatedParent, list));
            Assert.Equal(new Thickness(0, 0, 12, 0), scroll.Padding);
            var item = Assert.Single(list.GetVisualDescendants().OfType<ListBoxItem>());
            var card = Assert.Single(item.GetVisualDescendants().OfType<AppCard>());
            var presenter = Assert.Single(item.GetVisualDescendants().OfType<ContentPresenter>(), control => control.Name == "PART_ContentPresenter"
                && ReferenceEquals(control.TemplatedParent, item));
            Assert.Equal(new Thickness(0), item.Padding);
            Assert.Equal(Colors.Transparent, Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color);
            Assert.Equal(new Thickness(16), card.Padding);
            Assert.Equal(new CornerRadius(12), card.CornerRadius);
            Assert.Equal(new Thickness(0), card.BorderThickness);
            Assert.True(card.Bounds.Width > 260);
            var start = card.TranslatePoint(new Point(0, 0), list)!.Value;
            Assert.True(start.X >= 0);
            Assert.True(start.X + card.Bounds.Width <= list.Bounds.Width);
            Assert.All(card.GetVisualDescendants().OfType<AppIcon>(), icon => Assert.Equal(14, icon.Size));
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(980, 640, false)]
    [InlineData(1280, 820, false)]
    [InlineData(980, 640, true)]
    [InlineData(1280, 820, true)]
    public Task Editor_UsesSharedGeometryAndKeepsFooterInsideSmallWindows(int width, int height, bool dark) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        fixture.Main.Pages.OpenAutomationsCommand.Execute(null);
        await fixture.Main.Pages.Automations!.CreateCommand.ExecuteAsync(null);
        var window = Window(fixture, width, height, dark);
        window.Show();
        try
        {
            window.UpdateLayout();
            var dialog = Assert.Single(window.GetVisualDescendants().OfType<AppSettingsDialog>(), control => control.IsOpen);
            var surface = Assert.Single(dialog.GetVisualDescendants().OfType<AppCard>(), control => control.Name == "PART_Dialog");
            var prompt = Assert.Single(dialog.GetVisualDescendants().OfType<AppInput>(), control => control.Classes.Contains("automation-prompt"));
            Assert.Equal(540, surface.Width);
            Assert.Equal(96, prompt.MinHeight);
            Assert.Equal(160, prompt.MaxHeight);
            Assert.Equal(Avalonia.Layout.VerticalAlignment.Top, prompt.VerticalContentAlignment);
            var origin = surface.TranslatePoint(new Point(0, 0), window)!.Value;
            Assert.True(origin.Y >= 44);
            Assert.True(origin.Y + surface.Bounds.Height <= height);
            var save = Assert.Single(dialog.GetVisualDescendants().OfType<AppButton>(), button => button.Command == fixture.Main.Pages.Automations.SaveCommand);
            var bottom = save.TranslatePoint(new Point(0, save.Bounds.Height), window)!.Value;
            Assert.True(bottom.Y <= height);
            Assert.True(save.IsEffectivelyVisible);
            Assert.Empty(fixture.Server.Requests);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static MainWindow Window(WorkspaceModelSelectionFixture fixture, int width, int height, bool dark) => new()
    {
        DataContext = fixture.Main,
        WindowState = WindowState.Normal,
        Width = width,
        Height = height,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static AutomationSchedule Schedule(WorkspaceModelSelectionFixture fixture) => new(Guid.NewGuid(), "合成代码审查",
        "检查项目，不修改文件。", fixture.Workspace.Catalog.SelectedProject!.Id, fixture.SelectedProvider.Id, fixture.SelectedModel.Id,
        AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday, TimeZoneInfo.Local.Id, ApprovalPolicy.ReadOnly, 30, false,
        DateTimeOffset.UtcNow.AddDays(1));
}
