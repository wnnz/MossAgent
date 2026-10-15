using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelPickerComponentStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "ready")]
    [InlineData(true, "ready")]
    [InlineData(false, "loading")]
    [InlineData(true, "loading")]
    [InlineData(false, "error")]
    [InlineData(true, "error")]
    [InlineData(false, "unavailable")]
    [InlineData(true, "unavailable")]
    public Task RealSharedView_ShowsStateAndAlignsEnabledModelOptions(bool dark, string state) => _session.DispatchAsync(async () =>
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "alternative", DisplayName = "Alternative", IsDefault = false });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        Task? pending = null;
        TaskCompletionSource<IReadOnlyList<ModelProfile>>? delayed = null;
        if (state == "loading")
        {
            delayed = new(TaskCreationOptions.RunContinuationsAsynchronously);
            repository.ModelLoader = _ => delayed.Task;
            pending = picker.ReloadAsync();
        }
        else if (state == "error")
        {
            repository.ModelLoader = _ => Task.FromException<IReadOnlyList<ModelProfile>>(
                new InvalidOperationException("fixture-secret-error"));
            await picker.ReloadAsync();
        }
        else if (state == "unavailable")
        {
            repository.Models.Remove(model);
            await picker.ReloadAsync();
        }
        picker.ShowModelListCommand.Execute(null);
        var view = new ModelPickerFlyoutView { DataContext = picker };
        var window = new AppWindow
        {
            Width = 280,
            Height = 420,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var notice = Assert.Single(view.GetVisualDescendants().OfType<AppText>(), text => text.Text == picker.CatalogNotice);
            Assert.Equal(state != "ready", notice.IsVisible);
            Assert.InRange(notice.Bounds.Width, 0, 207);
            Assert.DoesNotContain("fixture-secret-error", notice.Text!, StringComparison.Ordinal);
            AssertModelRows(view, state);
        }
        finally
        {
            window.Close();
            picker.Dispose();
            delayed?.TrySetResult([model]);
            if (pending is not null) await pending;
        }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task QuickMenu_UsesSharedActionLayoutAndActuallyOpensModelList(bool dark) => _session.DispatchAsync(async () =>
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.ShowQuickMenuCommand.Execute(null);
        var view = new ModelPickerFlyoutView { DataContext = picker };
        var window = new AppWindow
        {
            Width = 280,
            Height = 420,
            Content = view,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            window.UpdateLayout();
            var action = Assert.Single(view.GetVisualDescendants().OfType<AppActionButton>(),
                button => button.Classes.Contains("model-picker-nav"));
            Assert.True(action.IsEffectivelyVisible);
            Assert.Equal(picker.ReasoningDisplayName, action.Label);
            Assert.Equal("选择模型", AutomationProperties.GetName(action));
            Assert.Equal(12, action.FontSize);
            Assert.Equal(FontWeight.SemiBold, action.FontWeight);
            var icon = Assert.Single(action.GetVisualDescendants().OfType<AppIcon>(), icon => icon.IsVisible);
            Assert.Equal(12, icon.Size);
            AssertIconAlignment(action, icon, picker.ReasoningDisplayName);

            action.Command!.Execute(action.CommandParameter);

            Assert.True(picker.IsModelListVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void AssertModelRows(ModelPickerFlyoutView view, string state)
    {
        var rows = view.GetVisualDescendants().OfType<AppActionButton>()
            .Where(button => button.Classes.Contains("model-option")).ToArray();
        Assert.Equal(state == "unavailable" ? 1 : 2, rows.Length);
        foreach (var row in rows)
        {
            var item = Assert.IsType<ModelPickerItemViewModel>(row.CommandParameter);
            Assert.Equal(item.AccessibleName, AutomationProperties.GetName(row));
            Assert.Equal(state is "ready" or "unavailable", row.IsEffectivelyEnabled);
            Assert.Equal(12, row.FontSize);
            Assert.Equal(item.IsSelected, row.IsTrailingIconVisible);
            var icons = row.GetVisualDescendants().OfType<AppIcon>().Where(icon => icon.IsVisible).ToArray();
            Assert.Equal(item.IsSelected ? 2 : 1, icons.Length);
            foreach (var icon in icons)
            {
                Assert.Equal(12, icon.Size);
                AssertIconAlignment(row, icon, item.DisplayName);
            }
        }
    }

    private static void AssertIconAlignment(AppActionButton action, AppIcon icon, string label)
    {
        var text = Assert.Single(action.GetVisualDescendants().OfType<AppText>(), text => text.Text == label);
        var labelPosition = text.TranslatePoint(default, action)!.Value;
        var position = icon.TranslatePoint(default, action)!.Value;
        Assert.InRange(Math.Abs(position.Y + icon.Bounds.Height / 2
            - labelPosition.Y - text.Bounds.Height / 2), 0, 0.5);
    }
}
