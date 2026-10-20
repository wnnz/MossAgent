using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Input.TextInput;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerInputKeyboardTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public Task HoldingEnterSubmitsOnceUntilKeyRelease(RawInputModifiers modifiers) => WithInput((window, input, submitted) =>
    {
        KeyDown(window, modifiers);
        for (var repeat = 0; repeat < 8; repeat++) KeyDown(window, modifiers);
        Assert.Equal("draft", Assert.Single(submitted));
        Assert.Equal("draft", input.Text);
        KeyUp(window, modifiers);
        Press(window, modifiers);
        Assert.Equal(2, submitted.Count);
    });

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public Task EligibilityCannotActivateAnAlreadyHeldKey(RawInputModifiers modifiers) => WithInput((window, input, submitted) =>
    {
        input.IsSubmissionEnabled = false;
        KeyDown(window, modifiers);
        input.IsSubmissionEnabled = true;
        KeyDown(window, modifiers);
        Assert.Empty(submitted);
        KeyUp(window, modifiers);
        Press(window, modifiers);
        Assert.Single(submitted);
    });

    [Theory]
    [InlineData("blank")]
    [InlineData("read-only")]
    [InlineData("disabled")]
    [InlineData("hidden")]
    [InlineData("unfocused")]
    [InlineData("detached")]
    [InlineData("no-command")]
    [InlineData("disabled-command")]
    [InlineData("unavailable")]
    public Task IneligibleInputCannotSubmitOrMutateDraft(string state) => WithInput((window, input, submitted) =>
    {
        switch (state)
        {
            case "blank": input.Text = " \t\r\n"; break;
            case "read-only": input.IsReadOnly = true; break;
            case "disabled": input.IsEnabled = false; break;
            case "hidden": input.IsVisible = false; break;
            case "unfocused": window.FocusManager!.Focus(null); break;
            case "detached": window.Content = null; break;
            case "no-command": input.SubmitCommand = null; break;
            case "disabled-command": input.SubmitCommand = new RelayCommand(() => submitted.Add("invalid"), () => false); break;
            case "unavailable": input.IsSubmissionEnabled = false; break;
        }
        var draft = input.Text;
        foreach (var modifiers in new[] { KeyModifiers.None, KeyModifiers.Control })
        {
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = modifiers });
            input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Enter, KeyModifiers = modifiers });
        }
        Assert.Empty(submitted);
        Assert.Equal(draft, input.Text);
    });

    [Theory]
    [InlineData(RawInputModifiers.Shift)]
    [InlineData(RawInputModifiers.Alt)]
    [InlineData(RawInputModifiers.Control | RawInputModifiers.Shift)]
    [InlineData(RawInputModifiers.Meta)]
    public Task ModifiedEnterDoesNotBecomeSubmissionWhileHeld(RawInputModifiers modifiers) => WithInput((window, input, submitted) =>
    {
        KeyDown(window, modifiers);
        KeyDown(window);
        Assert.Empty(submitted);
        KeyUp(window);
        Press(window, RawInputModifiers.None);
        Assert.Single(submitted);
    });

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public Task DefaultTextBoxImeClientCannotSubmitPreeditOrHeldCommit(RawInputModifiers modifiers) => WithInput((window, input, submitted) =>
    {
        var request = new TextInputMethodClientRequestedEventArgs { RoutedEvent = InputElement.TextInputMethodClientRequestedEvent };
        input.RaiseEvent(request);
        var client = Assert.IsAssignableFrom<TextInputMethodClient>(request.Client);
        Assert.True(client.SupportsPreedit);
        client.SetPreeditText("ni hao", 3);
        var presenter = Assert.Single(input.GetVisualDescendants().OfType<TextPresenter>());
        Assert.Equal("ni hao", presenter.PreeditText);
        input.RaiseEvent(new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Key = Key.Enter,
            KeyModifiers = modifiers == RawInputModifiers.Control ? KeyModifiers.Control : KeyModifiers.None
        });
        Assert.Empty(submitted);
        client.SetPreeditText(null);
        window.KeyTextInput("你好");
        var committed = input.Text;
        KeyDown(window, modifiers);
        Assert.Empty(submitted);
        Assert.Equal(committed, input.Text);
        KeyUp(window, modifiers);
        Press(window, modifiers);
        Assert.Equal(committed, Assert.Single(submitted));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task FocusAndAttachmentChangesDoNotLeaveInputStuck(bool detach) => WithInput((window, input, submitted) =>
    {
        KeyDown(window);
        Assert.Single(submitted);
        if (detach) window.Content = null;
        else window.FocusManager!.Focus(null);
        KeyUp(window);
        if (detach) window.Content = input;
        window.UpdateLayout();
        Assert.True(input.Focus());
        Press(window, RawInputModifiers.None);
        Assert.Equal(2, submitted.Count);
    });

    [Fact]
    public Task AlternateSubmissionUsesItsOwnCommandAndStillRejectsHeldKeys() => WithInput((window, input, submitted) =>
    {
        var alternate = 0;
        input.AlternateSubmitCommand = new RelayCommand(() => alternate++);
        KeyDown(window, RawInputModifiers.Control);
        KeyDown(window, RawInputModifiers.Control);
        Assert.Equal(1, alternate);
        Assert.Empty(submitted);
        KeyUp(window, RawInputModifiers.Control);
        Press(window, RawInputModifiers.None);
        Assert.Single(submitted);
    });

    [Fact]
    public Task UnavailableAlternateCommandCannotFallBackToTheDefaultAction() => WithInput((window, input, submitted) =>
    {
        input.AlternateSubmitCommand = new RelayCommand(() => submitted.Add("invalid"), () => false);
        Press(window, RawInputModifiers.Control);
        Assert.Empty(submitted);
        Assert.Equal("draft", input.Text);
        Press(window, RawInputModifiers.None);
        Assert.Single(submitted);
    });

    private Task WithInput(Action<AppWindow, AppComposerInput, List<string>> action) => _session.Dispatch(() =>
    {
        var submitted = new List<string>();
        var input = new AppComposerInput { Text = "draft", AcceptsReturn = true, IsSubmissionEnabled = true };
        input.SubmitCommand = new RelayCommand(() => submitted.Add(input.Text!));
        var window = new AppWindow { Width = 500, Height = 250, Content = input };
        window.Show();
        try
        {
            window.UpdateLayout();
            Assert.True(input.Focus());
            action(window, input, submitted);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Press(Window window, RawInputModifiers modifiers)
    {
        KeyDown(window, modifiers);
        KeyUp(window, modifiers);
    }

    private static void KeyDown(Window window, RawInputModifiers modifiers = RawInputModifiers.None) =>
        window.KeyPress(Key.Enter, modifiers, PhysicalKey.Enter, "\r");

    private static void KeyUp(Window window, RawInputModifiers modifiers = RawInputModifiers.None) =>
        window.KeyRelease(Key.Enter, modifiers, PhysicalKey.Enter, "\r");
}
