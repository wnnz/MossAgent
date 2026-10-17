using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using Xunit;

namespace MossAgent.App.Tests;

internal static class SubagentApprovalInteraction
{
    public static MainWindow Show(MainWindowViewModel main, bool dark)
    {
        var window = new MainWindow
        {
            DataContext = main,
            Width = dark ? 526 : 1280,
            Height = dark ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        Flush(window);
        return window;
    }

    public static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
    }

    public static async Task WaitUntilAsync(Func<bool> condition, CancellationToken token)
    {
        while (!condition()) await Task.Delay(10, token);
    }

    public static ToolApprovalView View(MainWindow window) => Assert.Single(window.GetVisualDescendants().OfType<ToolApprovalView>());

    public static void Decide(MainWindow window, ApprovalViewModel approval, bool approve, bool keyboard)
    {
        Flush(window);
        var view = View(window);
        Assert.True(view.IsEffectivelyVisible);
        var decision = Assert.Single(view.GetVisualDescendants().OfType<AppApprovalButton>(),
            button => ReferenceEquals(button.Command, approve ? approval.ApproveCommand : approval.DenyCommand));
        Assert.Equal(approval.CurrentRequestId, decision.CommandParameter);
        Activate(window, decision, keyboard);
    }

    public static void Activate(MainWindow window, AppButton button, bool keyboard)
    {
        button.BringIntoView();
        Flush(window);
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        var point = AssertOnScreen(window, button);
        if (keyboard)
        {
            Assert.True(button.Focus(NavigationMethod.Tab));
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        }
        else
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Flush(window);
    }

    public static void AssertVisibleDialogue(MainWindow window, ChatMessageViewModel row, bool keyboard)
    {
        Flush(window);
        var turn = Assert.Single(window.GetVisualDescendants().OfType<AppConversationTurn>(),
            control => control.Turn?.Activities.Contains(row) == true);
        if (!turn.Turn!.IsExpanded)
            Activate(window, Assert.Single(turn.GetVisualDescendants().OfType<AppActionButton>(),
                button => ReferenceEquals(button.Command, turn.Turn.ToggleCommand)), keyboard);
        Flush(window);
        var activity = Assert.Single(turn.GetVisualDescendants().OfType<AppToolActivityRow>(),
            control => ReferenceEquals(control.Command, row.ToggleToolCommand));
        var toggle = activity.FindControl<AppButton>("ToolButton")!;
        if (!row.IsToolExpanded) Activate(window, toggle, keyboard);
        var dialogue = Assert.Single(turn.GetVisualDescendants().OfType<SubagentDialogueView>(), view => ReferenceEquals(view.DataContext, row));
        Flush(window);
        Assert.True(dialogue.IsEffectivelyVisible);
        var heading = Assert.Single(dialogue.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
            text => text.IsEffectivelyVisible && text.Markdown == "子代理结果");
        Activate(window, toggle, keyboard);
        Assert.False(dialogue.IsEffectivelyVisible);
        Activate(window, toggle, keyboard);
        Assert.True(dialogue.IsEffectivelyVisible);
        foreach (var viewer in heading.GetVisualAncestors().OfType<AppScrollViewer>())
        {
            var origin = heading.TranslatePoint(default, viewer)!.Value;
            var inset = Math.Max(0, (viewer.Viewport.Height - heading.Bounds.Height) / 2);
            viewer.Offset = new Vector(viewer.Offset.X, viewer.Offset.Y + origin.Y - inset);
            Flush(window);
        }
        AssertOnScreen(window, heading, 4);
    }

    private static Point AssertOnScreen(MainWindow window, Control control, double? horizontalInset = null)
    {
        var point = control.TranslatePoint(new Point(horizontalInset ?? control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        Assert.True(new Rect(window.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
        Assert.True(ReferenceEquals(control, hit) || control.IsVisualAncestorOf(hit),
            $"Expected {control.GetType().Name}, hit {hit.GetType().Name}.");
        return point;
    }

    public static void Save(MainWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_VISUAL_EVIDENCE_DIR");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        Flush(window);
        using var bitmap = window.CaptureRenderedFrame();
        Assert.NotNull(bitmap);
        bitmap.Save(Path.Combine(directory, name + ".png"), PngBitmapEncoderOptions.Default);
    }
}
