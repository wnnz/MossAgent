using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ModelPickerSearchVisualTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, "target", "unique")]
    [InlineData(true, "target", "  TARGET  ")]
    [InlineData(false, "fixture", "  FIXTURE  ")]
    [InlineData(true, "fixture", "  FIXTURE  ")]
    public Task EquivalentSearchInput_RetainsRenderedRowsFocusAndSelection(bool dark, string firstQuery, string secondQuery)
        => _session.DispatchAsync(async () =>
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var other = provider with { Id = Guid.NewGuid(), Name = "other", IsDefault = false };
        repository.Providers.Add(other);
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target", DisplayName = "unique" });
        repository.Models.Add(model with { Id = Guid.NewGuid(), ProviderId = other.Id, DisplayName = "Other" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
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
            Flush(window);
            var search = Assert.Single(view.GetVisualDescendants().OfType<AppInput>());
            Assert.True(search.Focus(NavigationMethod.Tab));
            window.KeyTextInput(firstQuery);
            Flush(window);
            var rows = ModelRows(view);
            var selected = picker.SelectedItem;
            search.SelectAll();

            window.KeyTextInput(secondQuery);
            Flush(window);

            Assert.Equal(secondQuery, picker.SearchQuery);
            Assert.Equal(rows, ModelRows(view));
            Assert.True(search.IsFocused);
            Assert.Same(selected, picker.SelectedItem);
            var target = Assert.Single(rows, row => row.Label == "unique");
            target.Command!.Execute(target.CommandParameter);
            Flush(window);
            Assert.Equal("target", picker.SelectedItem?.ModelId);
            Assert.True(picker.CanUseSelectedModel);
            Assert.True(picker.IsQuickMenuVisible);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppActionButton[] ModelRows(ModelPickerFlyoutView view) => view.GetVisualDescendants()
        .OfType<AppActionButton>().Where(button => button.Classes.Contains("model-option")).ToArray();

    private static void Flush(AppWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
