using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class HeaderStateSynchronizationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task InspectorHeader_TracksTabAliasesAndCommandReplacement(bool dark) => WithWindow(() => new AppInspectorHeader(), dark, header =>
    {
        var tabs = header.GetVisualDescendants().OfType<AppIconButton>()
            .Where(button => button.Classes.Contains("inspector-tab")).ToArray();
        Assert.Equal(5, tabs.Length);
        var expansion = header.FindControl<AppIconButton>("ExpansionButton")!;
        var back = header.FindControl<AppIconButton>("BackButton")!;
        var close = header.FindControl<AppIconButton>("CloseButton")!;
        Assert.False(expansion.IsVisible);
        Assert.False(expansion.IsEnabled);
        var canExecute = true;
        var command = new RelayCommand(() => { }, () => canExecute);
        header.SelectCommand = command;
        header.BackCommand = command;
        header.CloseCommand = command;
        header.ExpansionCommand = command;
        Assert.All(tabs, button => Assert.True(button.IsEffectivelyEnabled));
        foreach (var tab in new string?[] { "home", "sources", "diff", "terminal", "browser", "audit", "unknown", null })
        {
            header.ActiveTab = tab;
            var selected = tabs.Where(button => button.Classes.Contains("selected")).ToArray();
            if (tab is "unknown" or null) Assert.Empty(selected);
            else Assert.Equal(tab == "home" ? "sources" : tab, Assert.Single(selected).CommandParameter);
            Assert.Equal(tab is not ("home" or "sources"), back.IsVisible);
        }
        header.IsExpanded = true;
        Assert.True(expansion.IsAlternateIcon);
        Assert.Equal(InspectorText.PreviewRestore, expansion.Label);
        header.IsExpanded = false;
        Assert.False(expansion.IsAlternateIcon);
        Assert.Equal(InspectorText.PreviewExpand, expansion.Label);
        canExecute = false;
        command.NotifyCanExecuteChanged();
        Assert.All(header.GetVisualDescendants().OfType<AppIconButton>(), button => Assert.False(button.IsEffectivelyEnabled));
        header.SelectCommand = null;
        header.BackCommand = null;
        header.CloseCommand = null;
        header.ExpansionCommand = null;
        canExecute = true;
        command.NotifyCanExecuteChanged();
        Assert.All(tabs, button => Assert.False(button.IsEffectivelyEnabled));
        Assert.False(back.IsEnabled);
        Assert.False(close.IsEnabled);
        Assert.False(expansion.IsVisible);
        Assert.False(expansion.IsEnabled);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ConversationHeader_TracksActionsAndIndependentStatusFlags(bool dark) => WithWindow(() => new AppConversationHeader(), dark, header =>
    {
        var command = new RelayCommand(() => { });
        var names = new[] { "ChatMoreButton", "ChatContextButton", "ChatInspectorButton" };
        var buttons = names.Select(name => header.FindControl<AppIconButton>(name)!).ToArray();
        var expansion = header.FindControl<AppActionButton>("ChatExpandInspectorButton")!;
        header.MenuContent = new AppText { Text = "synthetic menu" };
        header.ToggleContextCommand = command;
        header.ToggleInspectorCommand = command;
        header.ExpandInspectorCommand = command;
        Assert.False(header.HasConversation);
        Assert.All(buttons, button => Assert.False(button.IsEnabled));
        Assert.False(expansion.IsEnabled);
        header.ConversationTarget = new object();
        Assert.True(header.HasConversation);
        Assert.All(buttons, button => Assert.True(button.IsEffectivelyEnabled));
        Assert.True(expansion.IsEnabled);
        header.HasStartupStatus = true;
        Assert.True(header.HasStatus);
        var status = header.FindControl<Grid>("StatusLayout")!;
        Assert.True(status.ColumnDefinitions[0].Width.IsStar);
        Assert.Equal(0, status.ColumnDefinitions[2].Width.Value);
        header.IsRunning = true;
        header.HasStartupStatus = false;
        Assert.True(header.HasStatus);
        Assert.Equal(0, status.ColumnDefinitions[0].Width.Value);
        Assert.True(status.ColumnDefinitions[2].Width.IsStar);
        header.HasStartupWarning = true;
        header.IsRunning = false;
        Assert.True(header.HasStatus);
        var startup = header.FindControl<AppButton>("StartupSettingsButton")!;
        Assert.False(startup.IsEnabled);
        header.StartupActionCommand = command;
        Assert.True(startup.IsEffectivelyEnabled);
        header.HasStartupWarning = false;
        Assert.False(header.HasStatus);
        header.MenuContent = null;
        header.ToggleContextCommand = null;
        header.ToggleInspectorCommand = null;
        header.ExpandInspectorCommand = null;
        header.StartupActionCommand = null;
        Assert.All(buttons, button => Assert.False(button.IsEnabled));
        Assert.False(expansion.IsEnabled);
        Assert.False(startup.IsEnabled);
        header.ConversationTarget = null;
        Assert.False(header.HasConversation);
        Assert.All(buttons, button => Assert.False(button.IsEffectivelyVisible));
    });

    private Task WithWindow<TControl>(Func<TControl> createControl, bool dark, Action<TControl> action) where TControl : AppUserControl => _session.Dispatch(() =>
    {
        var control = createControl();
        var window = new AppWindow
        {
            Width = 640,
            Height = 220,
            Content = control,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); action(control); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
