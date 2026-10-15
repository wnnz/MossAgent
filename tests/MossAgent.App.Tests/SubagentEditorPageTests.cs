using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SubagentEditorPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FailureStatus_WrapsBelowTheSharedDialogFooterActions(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SubagentEditorTestFixture();
        fixture.Repository.BeforeSave = _ => Task.FromException(new InvalidOperationException("fixture-secret-error"));
        fixture.Settings.IsFormOpen = true;
        var pane = new SubagentSettingsPane { DataContext = fixture.Settings };
        var window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            await fixture.Settings.SaveCommand.ExecuteAsync(null);
            window.UpdateLayout();
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(),
                button => Equals(button.Content, "保存子代理"));
            var status = Assert.Single(pane.GetVisualDescendants().OfType<AppText>(),
                text => text.IsEffectivelyVisible && text.Text == fixture.Settings.Status);
            Assert.True(fixture.Settings.HasStatusError);
            Assert.DoesNotContain("fixture-secret-error", status.Text);
            Assert.True(save.Bounds.Width > 200);
            Assert.True(status.Bounds.Top >= save.Bounds.Bottom);
            Assert.InRange(Math.Abs(status.Bounds.Left - save.Bounds.Left), 0, 0.5);
            var dialog = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsDialog>());
            var footer = Assert.IsType<Avalonia.Controls.StackPanel>(dialog.Footer);
            Assert.InRange(status.Bounds.Right, 0, footer.Bounds.Width + 0.5);
            Assert.Equal(Avalonia.Media.TextWrapping.Wrap, status.TextWrapping);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, 980, 640)]
    [InlineData(true, 980, 640)]
    [InlineData(false, 1280, 820)]
    [InlineData(true, 1280, 820)]
    public Task Refresh_RestoresVisibleSelectionsWithoutReloadingDraft(bool dark, int width, int height) => _session.DispatchAsync(async () =>
    {
        using var fixture = new SubagentEditorTestFixture();
        var settings = fixture.Settings;
        await settings.SaveCommand.ExecuteAsync(null);
        settings.OpenEditFormCommand.Execute(settings.SelectedProfile);
        var pane = new SubagentSettingsPane { DataContext = settings };
        var window = new AppWindow
        {
            Width = width,
            Height = height,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var modelSelector = Assert.Single(pane.GetVisualDescendants().OfType<AppComboBox>(),
                control => ReferenceEquals(control.ItemsSource, settings.Models));
            var list = Assert.Single(pane.GetVisualDescendants().OfType<AppListBox>());
            Assert.NotNull(modelSelector.SelectedItem);
            settings.Name = "unsaved name";
            settings.SystemPrompt = "unsaved prompt";
            var profile = settings.SelectedProfile!;
            fixture.Repository.Loader = _ => Task.FromResult<IReadOnlyList<SubagentProfile>>([profile with { }]);
            await settings.RefreshCommand.ExecuteAsync(null);
            window.UpdateLayout();

            Assert.Same(Assert.Single(settings.Models), settings.SelectedModel);
            Assert.Same(settings.SelectedModel, modelSelector.SelectedItem);
            Assert.Same(Assert.Single(settings.Items), settings.SelectedProfile);
            Assert.Same(settings.SelectedProfile, list.SelectedItem);
            Assert.Equal("unsaved name", settings.Name);
            Assert.Equal("unsaved prompt", settings.SystemPrompt);
            Assert.Equal(fixture.Model.Id, settings.SelectedModel!.Model.Id);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
