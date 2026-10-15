using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ConversationToolbarFidelityTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(360, false)]
    [InlineData(980, false)]
    [InlineData(1920, false)]
    [InlineData(360, true)]
    [InlineData(980, true)]
    [InlineData(1920, true)]
    public Task ClosedInspector_UsesThreeMeasuredActionsAndRoundedContextHighlight(int width, bool dark) => WithHeader(width, dark, (window, header) =>
    {
        var buttons = header.GetVisualDescendants().OfType<AppIconButton>().Where(button => button.IsEffectivelyVisible).ToArray();
        Assert.Equal(["ChatMoreButton", "ChatContextButton", "ChatInspectorButton"], buttons.Select(button => button.Name));
        Assert.All(buttons, button => Assert.Equal(new Size(32, 32), button.Bounds.Size));
        var centers = buttons.Select(button => button.TranslatePoint(new Point(16, 16), header)!.Value).ToArray();
        Assert.Equal([width - 102d, width - 66d, width - 26d], centers.Select(point => point.X));
        Assert.All(centers, point => Assert.Equal(26, point.Y));
        var context = Assert.Single(header.GetVisualDescendants().OfType<AppBorder>(), border => border.Classes.Contains("context-indicator"));
        Assert.Equal(new Size(28, 28), context.Bounds.Size);
        Assert.Equal(new CornerRadius(8), context.CornerRadius);
        Assert.Equal(12, Assert.Single(buttons[1].GetVisualDescendants().OfType<AppIcon>()).Size);
        Assert.Equal(10, Assert.Single(buttons[0].GetVisualDescendants().OfType<AppIcon>()).Size);
        Assert.Equal(10, Assert.Single(buttons[2].GetVisualDescendants().OfType<AppIcon>()).Size);
        Assert.True(header.TryFindResource("UiIconPanelAdd", out var geometry));
        Assert.Same(geometry, buttons[2].IconData);
        Assert.Equal(1, Assert.IsAssignableFrom<Geometry>(geometry).Bounds.Width / Assert.IsAssignableFrom<Geometry>(geometry).Bounds.Height);
        header.IsInspectorOpen = true;
        window.UpdateLayout();
        Assert.Equal(2, buttons.Count(button => button.IsEffectivelyVisible));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Expansion_RemainsKeyboardAccessibleInTheMenuAndClosesOnStateChange(bool dark) => WithHeader(520, dark, (window, header) =>
    {
        var calls = 0;
        header.ExpandInspectorCommand = new RelayCommand(() => { calls++; header.IsInspectorOpen = true; });
        var more = header.FindControl<AppIconButton>("ChatMoreButton")!;
        var flyout = Assert.IsType<AppFlyout>(more.Flyout);
        flyout.ShowAt(more);
        window.UpdateLayout();
        var expansion = Expansion(flyout);
        Assert.Same(header.ExpandInspectorCommand, expansion.Command);
        Assert.True(expansion.IsEffectivelyVisible);
        Activate(expansion);
        Assert.Equal(1, calls);
        Assert.True(header.IsInspectorOpen);
        Assert.False(flyout.IsOpen);
        header.IsInspectorOpen = false;
        flyout.ShowAt(more);
        window.UpdateLayout();
        header.ExpandInspectorCommand = new RelayCommand(() => { });
        Assert.False(flyout.IsOpen);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MissingTargetsAndBlockedCommands_DoNotExposeAnExecutableExpansion(bool dark) => WithHeader(520, dark, (window, header) =>
    {
        var calls = 0;
        var allowed = false;
        var command = new RelayCommand(() => calls++, () => allowed);
        header.ExpandInspectorCommand = command;
        var more = header.FindControl<AppIconButton>("ChatMoreButton")!;
        var flyout = Assert.IsType<AppFlyout>(more.Flyout);
        flyout.ShowAt(more);
        window.UpdateLayout();
        Assert.False(Expansion(flyout).IsEffectivelyEnabled);
        Assert.Equal(0, calls);
        allowed = true;
        command.NotifyCanExecuteChanged();
        Assert.True(Expansion(flyout).IsEffectivelyEnabled);
        header.ConversationTarget = null;
        window.UpdateLayout();
        Assert.False(flyout.IsOpen);
        Assert.False(more.IsEffectivelyEnabled);
        header.ConversationTarget = new object();
        header.ExpandInspectorCommand = null;
        flyout.ShowAt(more);
        window.UpdateLayout();
        Assert.False(Expansion(flyout).IsEffectivelyVisible);
    });

    [Fact]
    public Task ExpansionOnlyMenu_IsAvailableUntilTheInspectorOpens() => WithHeader(520, false, (window, header) =>
    {
        header.MenuContent = null;
        var more = header.FindControl<AppIconButton>("ChatMoreButton")!;
        Assert.True(more.IsEffectivelyEnabled);
        header.IsInspectorOpen = true;
        window.UpdateLayout();
        Assert.False(more.IsEffectivelyEnabled);
        header.IsInspectorOpen = false;
        window.UpdateLayout();
        Assert.True(more.IsEffectivelyEnabled);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ContextAction_TracksVisibilityThemeAndCommandAvailability(bool dark) => WithHeader(520, dark, (window, header) =>
    {
        var context = header.FindControl<AppIconButton>("ChatContextButton")!;
        var indicator = Assert.Single(header.GetVisualDescendants().OfType<AppBorder>(), border => border.Classes.Contains("context-indicator"));
        var allowed = true;
        var command = new RelayCommand(() => header.IsContextVisible = !header.IsContextVisible, () => allowed);
        header.ToggleContextCommand = command;
        foreach (var visible in new[] { true, false, true })
        {
            header.IsContextVisible = visible;
            window.UpdateLayout();
            Assert.Equal(visible, context.Classes.Contains("active"));
            Assert.Equal(visible, indicator.IsEffectivelyVisible);
            AssertContextColor(context, visible);
        }
        window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
        window.UpdateLayout();
        AssertContextColor(context, true);
        Activate(context);
        window.UpdateLayout();
        Assert.False(header.IsContextVisible);
        Assert.DoesNotContain("active", context.Classes);
        AssertContextColor(context, false);
        header.IsContextVisible = true;
        allowed = false;
        command.NotifyCanExecuteChanged();
        window.UpdateLayout();
        Assert.False(context.IsEffectivelyEnabled);
        AssertContextColor(context, false);
    });

    private static void AssertContextColor(AppIconButton button, bool active)
    {
        Assert.True(Avalonia.Application.Current!.TryGetResource(active ? "TextPrimaryBrush" : "TextMutedBrush",
            button.ActualThemeVariant, out var resource));
        var expected = Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color;
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(button.Foreground).Color);
        var icon = Assert.Single(button.GetVisualDescendants().OfType<AppIcon>());
        Assert.Equal(expected, Assert.IsAssignableFrom<ISolidColorBrush>(icon.Stroke).Color);
    }

    private Task WithHeader(int width, bool dark, Action<AppWindow, AppConversationHeader> action) => _session.Dispatch(() =>
    {
        var header = new AppConversationHeader
        {
            ConversationTarget = new object(),
            MenuContent = new AppText { Text = "Synthetic menu" },
            ToggleContextCommand = new RelayCommand(() => { }),
            ToggleInspectorCommand = new RelayCommand(() => { }),
            ExpandInspectorCommand = new RelayCommand(() => { }),
            IsContextVisible = true
        };
        var window = new AppWindow
        {
            Width = width,
            Height = 100,
            Content = header,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try { window.UpdateLayout(); action(window, header); }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppActionButton Expansion(AppFlyout flyout) => Assert.Single(
        Assert.IsType<AppCard>(flyout.Content).GetVisualDescendants().OfType<AppActionButton>(),
        button => button.Name == "ChatExpandInspectorButton");

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
