using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AgentSettingsDialogPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public Task CreateAndEditButtons_OpenSharedDialogAndEscapeRestoresFocus(bool child, bool dark) => _session.DispatchAsync(async () =>
    {
        using var agent = new AgentEditorTestFixture();
        using var subagent = new SubagentEditorTestFixture();
        var pane = CreatePane(child, agent, subagent);
        var createCommand = child ? subagent.Settings.OpenCreateFormCommand : agent.Settings.OpenCreateFormCommand;
        var saveCommand = child ? subagent.Settings.SaveCommand : agent.Settings.SaveCommand;
        var window = CreateWindow(pane, dark, 1080, 760);
        window.Show();
        try
        {
            window.UpdateLayout();
            var dialog = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsDialog>());
            Assert.False(dialog.IsOpen);
            Assert.DoesNotContain(pane.GetVisualDescendants().OfType<AppInput>(), input => input.IsEffectivelyVisible);
            var create = Assert.Single(pane.GetVisualDescendants().OfType<AppActionButton>(),
                button => ReferenceEquals(button.Command, createCommand));
            PressSpace(create);
            Flush(window);
            Assert.True(dialog.IsOpen);
            var name = Assert.Single(dialog.GetVisualDescendants().OfType<AppInput>(),
                input => Avalonia.Automation.AutomationProperties.GetName(input) == AgentSettingsLabels.Name);
            Assert.Same(name, window.FocusManager!.GetFocusedElement());
            dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            Flush(window);
            Assert.False(dialog.IsOpen);
            Assert.Same(create, window.FocusManager.GetFocusedElement());
            Assert.Empty(agent.Repository.Profiles);
            Assert.Empty(subagent.Repository.Profiles);
            if (child)
            {
                subagent.Settings.Name = "saved child";
                subagent.Settings.TriggerCondition = "review";
                subagent.Settings.SystemPrompt = "review code";
            }
            else
            {
                agent.Settings.Name = "saved agent";
                agent.Settings.SystemPrompt = "review code";
            }
            await saveCommand.ExecuteAsync(null);
            Flush(window);
            var row = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsItemRow>());
            var edit = Assert.Single(row.GetVisualDescendants().OfType<AppButton>(), button => Equals(button.Content, SettingsItemLabels.Edit));
            Assert.True(edit.IsEnabled);
            PressSpace(edit);
            Flush(window);
            Assert.True(dialog.IsOpen);
            Assert.Equal(child ? "saved child" : "saved agent", name.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(false, false, 980, 700)]
    [InlineData(false, true, 460, 520)]
    [InlineData(true, false, 460, 520)]
    [InlineData(true, true, 980, 700)]
    public Task DialogBodyScrollsWhileFooterStaysInsideTheViewport(bool child, bool dark, int width, int height) => _session.Dispatch(() =>
    {
        using var agent = new AgentEditorTestFixture();
        using var subagent = new SubagentEditorTestFixture();
        if (child) subagent.Settings.IsFormOpen = true;
        else agent.Settings.IsFormOpen = true;
        var pane = CreatePane(child, agent, subagent);
        var window = CreateWindow(pane, dark, width, height);
        window.Show();
        try
        {
            Flush(window);
            var dialog = Assert.Single(pane.GetVisualDescendants().OfType<AppSettingsDialog>());
            var surface = Assert.Single(dialog.GetVisualDescendants().OfType<AppCard>());
            var body = Assert.Single(dialog.GetVisualDescendants().OfType<AppScrollViewer>(), control => control.Name == "PART_DialogBody");
            var footer = Assert.IsType<StackPanel>(dialog.Footer);
            var saveCommand = child ? subagent.Settings.SaveCommand : agent.Settings.SaveCommand;
            var save = Assert.Single(footer.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command, saveCommand));
            Assert.True(save.IsEffectivelyVisible);
            Assert.InRange(surface.Bounds.Height, 1, height);
            Assert.InRange(surface.Bounds.Width, 1, width);
            var origin = save.TranslatePoint(default, window)!.Value;
            Assert.InRange(origin.X, 0, width - save.Bounds.Width);
            Assert.InRange(origin.Y, 0, height - save.Bounds.Height);
            if (height < 600) Assert.True(body.Extent.Height > body.Viewport.Height);
            body.Offset = new Vector(0, body.Extent.Height);
            Flush(window);
            Assert.Equal(origin, save.TranslatePoint(default, window)!.Value);
            SaveFrame(window, child, dark, width, height);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static AppUserControl CreatePane(bool child, AgentEditorTestFixture agent, SubagentEditorTestFixture subagent) => child
        ? new SubagentSettingsPane { DataContext = subagent.Settings }
        : new AgentSettingsPane { DataContext = agent.Settings };

    private static AppWindow CreateWindow(Control pane, bool dark, int width, int height) => new()
    {
        Width = width,
        Height = height,
        Content = pane,
        RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
    };

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Avalonia.Headless.AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static void SaveFrame(AppWindow window, bool child, bool dark, int width, int height)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_TEST_SCREENSHOTS");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{(child ? "subagent" : "agent")}-{(dark ? "dark" : "light")}-{width}x{height}.png"), PngBitmapEncoderOptions.Default);
    }
}
