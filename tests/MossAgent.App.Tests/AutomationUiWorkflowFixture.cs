using System.Windows.Input;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class AutomationUiWorkflowFixture : IAsyncDisposable
{
    private readonly WorkspaceModelSelectionFixture _core;

    private AutomationUiWorkflowFixture(WorkspaceModelSelectionFixture core, bool dark, int width)
    {
        _core = core;
        Window = new MainWindow
        {
            DataContext = core.Main,
            Width = width,
            Height = width < 1000 ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        Flush();
    }

    public MainWindow Window { get; }
    public WorkspaceModelSelectionFixture Core => _core;
    public AutomationViewModel Page => _core.Main.Pages.Automations!;
    public CancellationToken Token => _core.Token;
    public bool Keyboard => Window.Width < 1000;

    public static async Task<AutomationUiWorkflowFixture> CreateAsync(bool dark, int width)
    {
        var core = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse: true);
        AutomationUiWorkflowFixture? fixture = null;
        try
        {
            await core.Scheduler.StartAsync(core.Token);
            fixture = new AutomationUiWorkflowFixture(core, dark, width);
            fixture.OpenPage();
            await fixture.WaitUntilAsync(() => !fixture.Page.List.IsLoading);
            Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, fixture.Window.ActualThemeVariant);
            return fixture;
        }
        catch { fixture?.Window.Close(); await core.DisposeAsync(); throw; }
    }

    public void OpenPage()
    {
        var rail = Assert.Single(Window.GetVisualDescendants().OfType<AppIconButton>(),
            button => button.IsEffectivelyVisible && button.Classes.Contains("navigation-rail-button") && button.Label == AutomationText.Title);
        Activate(rail);
        Assert.True(Core.Main.Pages.IsAutomationsOpen);
    }

    public AppButton Button(ICommand command, string? text = null) => Assert.Single(Window.GetVisualDescendants().OfType<AppButton>(),
        button => button.IsEffectivelyVisible && ReferenceEquals(button.Command, command)
            && (text is null || Equals(button.Content, text)));

    public void Activate(AppButton button) => SubagentApprovalInteraction.Activate(Window, button, Keyboard);

    public async Task ActivateAsync(IAsyncRelayCommand command)
    {
        Activate(Button(command));
        Assert.NotNull(command.ExecutionTask);
        await command.ExecutionTask.WaitAsync(Token);
        await WaitUntilAsync(() => !Page.IsBusy && !Page.List.IsLoading && !Page.History.IsLoading);
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            Flush();
            await Task.Delay(15, Token);
        }
        Flush();
    }

    public void Flush() => ProviderUiWorkflowInput.Flush(Window);
    public void Capture(string state)
    {
        var name = $"automation-ui-{Window.ActualThemeVariant}-{Window.Width}-{state}";
        SubagentApprovalInteraction.Save(Window, name);
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        var cards = Window.GetVisualDescendants().OfType<AppCard>().Where(card => card.Classes.Contains("automation-card"));
        var geometry = cards.Select(card => new
        {
            Origin = card.TranslatePoint(default, Window).ToString(),
            Bounds = card.Bounds.ToString(),
            Corners = card.CornerRadius.ToString(),
            Shadow = card.BoxShadow.ToString(),
            Ancestors = card.GetVisualAncestors().OfType<Control>().Select(control => new
            {
                Type = control.GetType().Name,
                Origin = control.TranslatePoint(default, Window).ToString(),
                Bounds = control.Bounds.ToString(),
                control.ClipToBounds
            }).ToArray()
        }).ToArray();
        File.WriteAllText(Path.Combine(directory, name + ".json"), JsonSerializer.Serialize(geometry, new JsonSerializerOptions { WriteIndented = true }));
    }

    public async ValueTask DisposeAsync()
    {
        try { Window.Close(); }
        finally { await _core.DisposeAsync(); }
    }
}
