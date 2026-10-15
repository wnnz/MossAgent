using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Models;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class ModelEditorStatusFixture : IDisposable
{
    public ConfigurationMutationProbeRepository Repository { get; } = ConfigurationMutationFailureTests.Repository();
    public ProviderSettingsViewModel Providers { get; }
    public ModelSettingsViewModel Models { get; }
    public ProviderAndModelSettingsPane Pane { get; }
    public AppWindow Window { get; }
    public AppSettingsDialog Dialog => Assert.Single(Pane.GetVisualDescendants().OfType<AppSettingsDialog>(),
        dialog => dialog.CloseLabel == "关闭模型配置");
    public string FooterStatus
    {
        get
        {
            Window.UpdateLayout();
            return ((Control)Dialog.Footer!).GetVisualDescendants().OfType<AppText>().Last().Text ?? string.Empty;
        }
    }

    public ModelEditorStatusFixture(bool dark, IModelDiscoveryService? discovery = null)
    {
        Repository.Store.Providers.Add(ProviderSettingsRowTestData.Provider() with { Name = "other-provider" });
        Providers = new ProviderSettingsViewModel(Repository);
        Models = new ModelSettingsViewModel(Repository, discovery ?? new FixedModelDiscoveryService([]), Providers);
        Pane = new ProviderAndModelSettingsPane { DataContext = new { Providers, Models } };
        Window = new AppWindow
        {
            Width = 980,
            Height = 700,
            Content = Pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Window.UpdateLayout();
    }

    public void OpenEditor(bool create)
    {
        if (create) Models.AddModelForProviderCommand.Execute(Models.ProviderGroups[1]);
        else Models.EditProviderModelCommand.Execute(Repository.Store.Models[0]);
        Assert.True(Models.IsFormOpen);
    }

    public async Task<string> ShowValidationErrorAsync()
    {
        Models.ModelId = string.Empty;
        await Models.SaveModelCommand.ExecuteAsync(null);
        Assert.True(Models.HasStatusError);
        Assert.NotEmpty(FooterStatus);
        return FooterStatus;
    }

    public void Dispose()
    {
        Window.Close();
        Models.Dispose();
        Providers.Dispose();
    }
}
