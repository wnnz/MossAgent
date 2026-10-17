using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

internal static class McpUiRecoveryReview
{
    public static void AssertVisibleResult(McpUiRecoveryFixture fixture)
    {
        var window = fixture.Window;
        ProviderUiWorkflowInput.Flush(window);
        var row = Assert.Single(fixture.Main.Workspace.Messages, message => message.ToolCallId == "mcp-flow");
        var turn = Assert.Single(window.GetVisualDescendants().OfType<AppConversationTurn>(),
            control => control.Turn?.Activities.Contains(row) == true);
        if (!turn.Turn!.IsExpanded)
            Activate(fixture, Assert.Single(turn.GetVisualDescendants().OfType<AppActionButton>(),
                button => ReferenceEquals(button.Command, turn.Turn.ToggleCommand)));
        var activity = Assert.Single(turn.GetVisualDescendants().OfType<AppToolActivityRow>(),
            control => ReferenceEquals(control.Command, row.ToggleToolCommand));
        if (!row.IsToolExpanded) Activate(fixture, activity.FindControl<AppButton>("ToolButton")!);
        ProviderUiWorkflowInput.Flush(window);
        AssertOnScreen(fixture, Assert.Single(turn.GetVisualDescendants().OfType<AppSelectableText>(),
            control => control.IsEffectivelyVisible && control.Text == row.Tool!.Details));
        fixture.SaveReview("tool-details");
        AssertOnScreen(fixture, Assert.Single(window.GetVisualDescendants().OfType<MarkdownSelectableTextBlock>(),
            control => control.IsEffectivelyVisible && control.Markdown == "MCP 链路验收已结束。"));
    }

    private static void Activate(McpUiRecoveryFixture fixture, AppButton button)
    {
        Assert.True(button.IsEffectivelyVisible);
        Assert.True(button.IsEffectivelyEnabled);
        Assert.True(button.Focus(NavigationMethod.Tab));
        AssertOnScreen(fixture, button);
        fixture.Window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        fixture.Window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
        ProviderUiWorkflowInput.Flush(fixture.Window);
    }

    private static void AssertOnScreen(McpUiRecoveryFixture fixture, Control control)
    {
        var window = fixture.Window;
        control.BringIntoView();
        ProviderUiWorkflowInput.Flush(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        ProviderUiWorkflowInput.Flush(window);
        using var renderedFrame = window.CaptureRenderedFrame();
        Assert.NotNull(renderedFrame);
        var point = control.TranslatePoint(new Point(4, control.Bounds.Height / 2), window)!.Value;
        Assert.True(new Rect(window.ClientSize).Contains(point));
        var hit = Assert.IsAssignableFrom<Visual>(window.InputHitTest(point));
        var matched = ReferenceEquals(control, hit) || control.IsVisualAncestorOf(hit);
        if (!matched) fixture.SaveReview("hit-failure");
        Assert.True(matched,
            $"Expected {control.GetType().Name}, hit {hit.GetType().Name} at {point}, bounds {control.Bounds}, hit-enabled={control.IsHitTestVisible}; "
            + $"after capture: {control.TranslatePoint(new Point(4, control.Bounds.Height / 2), window)} / {window.InputHitTest(point)?.GetType().Name}.");
    }
}
