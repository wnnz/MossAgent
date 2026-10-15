using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;

namespace MossAgent.App.Tests;

internal sealed class ModelPickerFocusFixture : IDisposable
{
    private ModelPickerFocusFixture(ModelPickerViewModel picker, bool dark)
    {
        Picker = picker;
        Menu = new ModelPickerFlyoutView { DataContext = picker };
        Flyout = new AppFlyout { Content = Menu };
        Draft = new AppInput { Text = "保留对话草稿" };
        Trigger = new AppButton { Content = "模型", Flyout = Flyout };
        Window = new AppWindow
        {
            Width = 980,
            Height = 700,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
            Content = new StackPanel { Children = { Draft, Trigger } }
        };
        Flyout.Closed += (_, _) => Picker.IsPickerOpen = false;
    }

    public ModelPickerViewModel Picker { get; }
    public ModelPickerFlyoutView Menu { get; }
    public AppFlyout Flyout { get; }
    public AppInput Draft { get; }
    public AppButton Trigger { get; }
    public AppWindow Window { get; }
    public AppInput Search => Menu.GetVisualDescendants().OfType<AppInput>().Single();
    public AppActionButton Navigation => Menu.GetVisualDescendants().OfType<AppActionButton>()
        .Single(button => button.Classes.Contains("model-picker-nav"));

    public static async Task<ModelPickerFocusFixture> CreateAsync(bool dark, bool showSearch = true)
    {
        var fixture = new ModelPickerFocusFixture(await CreatePickerAsync(showSearch), dark);
        fixture.Window.Show();
        fixture.Flush();
        return fixture;
    }

    public static async Task<ModelPickerViewModel> CreatePickerAsync(bool showSearch = true)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        if (showSearch)
        {
            var other = provider with { Id = Guid.NewGuid(), Name = "other", IsDefault = false };
            repository.Providers.Add(other);
            repository.Models.Add(model with { Id = Guid.NewGuid(), ProviderId = other.Id, ModelId = "alternative", DisplayName = "Alternative" });
        }
        var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        return picker;
    }

    public void Open(bool modelList = false)
    {
        Draft.Focus(NavigationMethod.Tab);
        if (modelList) Picker.ShowModelListCommand.Execute(null);
        else Picker.ShowQuickMenuCommand.Execute(null);
        Picker.IsPickerOpen = true;
        Flyout.ShowAt(Trigger);
        Flush();
    }

    public void EnterModelList()
    {
        Navigation.Focus(NavigationMethod.Tab);
        Navigation.Command!.Execute(Navigation.CommandParameter);
        Flush();
    }

    public void Flush()
    {
        Window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        Window.UpdateLayout();
    }

    public void Dispose()
    {
        Flyout.Hide();
        Window.Close();
        Picker.Dispose();
    }
}
