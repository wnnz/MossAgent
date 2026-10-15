using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ConversationHeaderComponentTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(360, false)]
    [InlineData(520, false)]
    [InlineData(980, false)]
    [InlineData(360, true)]
    [InlineData(520, true)]
    [InlineData(980, true)]
    public Task LongStatus_LeavesHeadingAndActionsBounded(double width, bool dark) => WithHeader((window, header) =>
    {
        Assert.Equal(width, header.Bounds.Width);
        window.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
        header.HasStartupStatus = true;
        header.HasStartupWarning = true;
        header.StartupStatus = new string('W', 200);
        header.IsRunning = true;
        header.Activity = new string('M', 200);
        window.UpdateLayout();
        var heading = Assert.IsType<AppIconLabel>(header.HeadingContent);
        Assert.True(heading.Bounds.Width >= 100);
        var buttons = ActionButtons(header);
        Assert.All(buttons, button =>
        {
            var bounds = Bounds(button, header);
            Assert.Equal(new Size(32, 32), button.Bounds.Size);
            Assert.True(bounds.Left >= 0 && bounds.Right <= header.Bounds.Width);
            Assert.Equal(26, bounds.Center.Y);
        });
        Assert.True(Bounds(heading, header).Right < Bounds(buttons[0], header).Left,
            $"Heading: {Bounds(heading, header)}; action: {Bounds(buttons[0], header)}; header: {header.Bounds}");
        foreach (var name in new[] { "StartupStatusText", "ActivityText" })
        {
            var text = header.FindControl<AppText>(name)!;
            Assert.True(text.Bounds.Width > 0);
            Assert.Equal(text.Text, ToolTip.GetTip(text));
            Assert.True(Bounds(text, header).Right < Bounds(buttons[0], header).Left);
        }
        header.HasStartupStatus = false;
        header.HasStartupWarning = false;
        header.IsRunning = false;
        window.UpdateLayout();
        Assert.False(header.HasStatus);
        Assert.Equal(26, Bounds(heading, header).Center.Y);
    }, width);

    [Fact]
    public Task OpenInspector_LeavesTwoHeaderActionsAndRestoresTheDockEntry() => WithHeader((window, header) =>
    {
        var buttons = ActionButtons(header);
        header.IsInspectorOpen = true;
        window.UpdateLayout();
        Assert.True(buttons[0].IsEffectivelyVisible);
        Assert.True(buttons[1].IsEffectivelyVisible);
        Assert.False(buttons[2].IsEffectivelyVisible);
        Assert.False(header.FindControl<AppBorder>("ChatInspectorSeparator")!.IsEffectivelyVisible);
        header.IsInspectorOpen = false;
        window.UpdateLayout();
        Assert.All(buttons, button => Assert.True(button.IsEffectivelyVisible));
        Assert.True(header.FindControl<AppBorder>("ChatInspectorSeparator")!.IsEffectivelyVisible);
    });

    [Fact]
    public Task ResizeAndLateStatus_PreserveContentCommandsAndBounds() => WithHeader((window, header) =>
    {
        var heading = header.HeadingContent;
        var command = header.ToggleInspectorCommand;
        var host = new Grid { Width = 360 };
        window.Content = host;
        host.Children.Add(header);
        foreach (var width in new[] { 360, 980, 520, 360 })
        {
            host.Width = width;
            header.IsRunning = true;
            header.Activity = new string('W', 200);
            header.HasStartupStatus = true;
            window.UpdateLayout();
            Assert.Equal(width, header.Bounds.Width);
            Assert.Same(heading, header.HeadingContent);
            Assert.Same(command, header.ToggleInspectorCommand);
            Assert.True(Bounds(Assert.IsType<AppIconLabel>(heading), header).Right
                < Bounds(ActionButtons(header)[0], header).Left);
            Assert.All(ActionButtons(header), button =>
                Assert.True(Bounds(button, header).Right <= header.Bounds.Width));
        }
    }, 1280);

    [Fact]
    public Task MissingTargetsAndCommands_DoNotExposeUsableActions() => WithHeader((window, header) =>
    {
        header.ConversationTarget = null;
        window.UpdateLayout();
        Assert.All(ActionButtons(header), button => Assert.False(button.IsEffectivelyVisible));
        header.ConversationTarget = new object();
        header.ToggleContextCommand = null;
        header.ToggleInspectorCommand = null;
        header.MenuContent = null;
        window.UpdateLayout();
        Assert.All(ActionButtons(header), button => Assert.False(button.IsEffectivelyEnabled));
    });

    [Fact]
    public Task Commands_RespectCanExecuteAndRouteKeyboardInput() => WithHeader((window, header) =>
    {
        var enabled = false;
        var count = 0;
        var command = new RelayCommand(() => count++, () => enabled);
        header.ToggleInspectorCommand = command;
        var button = header.FindControl<AppIconButton>("ChatInspectorButton")!;
        window.UpdateLayout();
        Assert.False(button.IsEffectivelyEnabled);
        enabled = true;
        command.NotifyCanExecuteChanged();
        Assert.True(button.IsEffectivelyEnabled);
        Activate(button);
        Assert.Equal(1, count);
        enabled = false;
        command.NotifyCanExecuteChanged();
        Assert.False(button.IsEffectivelyEnabled);
    });

    [Fact]
    public Task StartupAction_RemainsFunctionalWithoutAConversation() => WithHeader((window, header) =>
    {
        var count = 0;
        header.ConversationTarget = null;
        header.HasStartupStatus = true;
        header.HasStartupWarning = true;
        header.StartupActionCommand = new RelayCommand(() => count++);
        window.UpdateLayout();
        var button = header.FindControl<AppButton>("StartupSettingsButton")!;
        Assert.True(button.IsEffectivelyVisible);
        Activate(button);
        Assert.Equal(1, count);
        header.StartupActionCommand = null;
        Assert.False(button.IsEffectivelyEnabled);
    });

    [Theory]
    [InlineData("target")]
    [InlineData("content")]
    [InlineData("context")]
    [InlineData("command")]
    [InlineData("detach")]
    public Task Menu_InheritsBusinessDataAndClosesWhenOwnershipChanges(string change) => WithHeader((window, header) =>
    {
        header.DataContext = "business-context";
        var content = new AppText();
        content.Bind(TextBlock.TextProperty, new Binding("."));
        header.MenuContent = content;
        var button = header.FindControl<AppIconButton>("ChatMoreButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        flyout.ShowAt(button);
        window.UpdateLayout();
        Assert.True(flyout.IsOpen);
        var menuHost = Assert.Single(Assert.IsType<AppCard>(flyout.Content).GetVisualDescendants().OfType<AppContentControl>(),
            control => control.Classes.Contains("conversation-header-menu"));
        Assert.Equal(190, menuHost.Bounds.Width);
        Assert.Equal(Avalonia.Layout.HorizontalAlignment.Stretch, menuHost.HorizontalContentAlignment);
        Assert.Equal("business-context", content.Text);
        Assert.Same(header.DataContext, Assert.IsType<AppIconLabel>(header.HeadingContent).DataContext);
        switch (change)
        {
            case "target": header.ConversationTarget = new object(); break;
            case "content": header.MenuContent = new AppText(); break;
            case "context": header.DataContext = "other-context"; break;
            case "command": header.ToggleInspectorCommand = new RelayCommand(() => { }); break;
            case "detach": window.Content = null; break;
        }
        Assert.False(flyout.IsOpen);
    });

    private Task WithHeader(Action<AppWindow, AppConversationHeader> action, double width = 520) => _session.DispatchAsync(() =>
    {
        var heading = new AppIconLabel { Label = new string('W', 200) };
        heading.Classes.Add("conversation-heading");
        var header = new AppConversationHeader
        {
            HeadingContent = heading,
            ConversationTarget = new object(),
            MenuContent = new AppButton { Content = "Menu" },
            ToggleContextCommand = new RelayCommand(() => { }),
            ToggleInspectorCommand = new RelayCommand(() => { }),
            InspectorLabel = "Inspector",
            StartupStatus = "Warning",
            StartupActionLabel = "查看 MCP 设置",
            StartupActionCommand = new RelayCommand(() => { })
        };
        var window = new AppWindow { Width = width, Height = 150, Content = header };
        window.Show();
        try { window.UpdateLayout(); action(window, header); }
        finally { window.Close(); }
        return Task.CompletedTask;
    }, TestContext.Current.CancellationToken);

    private static AppIconButton[] ActionButtons(AppConversationHeader header) =>
        new[] { "ChatMoreButton", "ChatContextButton", "ChatInspectorButton" }
            .Select(name => header.FindControl<AppIconButton>(name)!).ToArray();

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    private static void Activate(AppButton button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }
}
