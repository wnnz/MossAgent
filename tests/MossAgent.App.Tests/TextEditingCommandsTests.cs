using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class TextEditingCommandsTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData("none", false, false)]
    [InlineData("noSelection", false, true)]
    [InlineData("readonly", true, true)]
    [InlineData("password", false, true)]
    [InlineData("empty", false, false)]
    [InlineData("hidden", false, false)]
    [InlineData("disabled", false, false)]
    [InlineData("detached", false, false)]
    [InlineData("ancestorHidden", false, false)]
    [InlineData("ancestorDisabled", false, false)]
    [InlineData("unrelatedFocus", false, false)]
    public Task Menu_UsesTheAvailableEditorAndRejectsInvalidTargets(string state, bool copy, bool selectAll) =>
        _session.Dispatch(() =>
        {
            var (window, menu, input, layout) = CreateWindow();
            try
            {
                if (state != "none")
                {
                    Assert.True(input.Focus());
                    input.SelectAll();
                }
                switch (state)
                {
                    case "noSelection": input.SelectionEnd = input.SelectionStart; break;
                    case "readonly": input.IsReadOnly = true; break;
                    case "password": input.PasswordChar = '*'; break;
                    case "empty": input.Text = string.Empty; break;
                    case "hidden": input.IsVisible = false; break;
                    case "disabled": input.IsEnabled = false; break;
                    case "detached": layout.Children.Remove(input); break;
                    case "ancestorHidden": layout.IsVisible = false; break;
                    case "ancestorDisabled": layout.IsEnabled = false; break;
                    case "unrelatedFocus":
                        var other = new AppButton { Content = "Other" };
                        layout.Children.Add(other);
                        Assert.True(other.Focus());
                        break;
                }

                Assert.Equal(copy, Row(menu, TextEditingAction.Copy).Command!.CanExecute(null));
                Assert.Equal(selectAll, Row(menu, TextEditingAction.SelectAll).Command!.CanExecute(null));
                Assert.False(Row(menu, TextEditingAction.Undo).Command!.CanExecute(null));
                Assert.False(Row(menu, TextEditingAction.Redo).Command!.CanExecute(null));
                if (state is not "noSelection")
                {
                    Assert.False(Row(menu, TextEditingAction.Cut).Command!.CanExecute(null));
                    var before = input.Text;
                    Row(menu, TextEditingAction.Cut).Command!.Execute(null);
                    Assert.Equal(before, input.Text);
                }
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PopupFocus_PreservesSelectionAndUsesTheHeadlessClipboard() => _session.DispatchAsync(async () =>
    {
        var (window, menu, input, _) = CreateWindow();
        try
        {
            var clipboard = Assert.IsAssignableFrom<IClipboard>(window.Clipboard);
            await clipboard.SetTextAsync("sentinel");
            Assert.True(input.Focus());
            input.SelectionStart = 0;
            input.SelectionEnd = 5;
            input.ClearSelectionOnLostFocus = true;
            Assert.True(menu.Focus());
            Assert.Equal(0, input.SelectionStart);
            Assert.Equal(5, input.SelectionEnd);
            Assert.IsType<AppFlyout>(menu.Flyout).ShowAt(menu);
            var copy = Row(menu, TextEditingAction.Copy);
            Assert.True(copy.Focus());
            Assert.True(copy.Command!.CanExecute(null));
            copy.Command.Execute(null);
            Assert.Equal("alpha", await clipboard.TryGetTextAsync());

            input.IsReadOnly = true;
            var paste = Row(menu, TextEditingAction.Paste).Command!;
            Assert.False(paste.CanExecute(null));
            paste.Execute(null);
            Assert.Equal("alpha beta", input.Text);

            input.IsReadOnly = false;
            await clipboard.SetTextAsync("updated");
            input.SelectAll();
            Assert.True(paste.CanExecute(null));
            paste.Execute(null);
            Assert.Equal("updated", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task UndoRedo_ExecuteRealEditorHistoryAndRecheckReadOnly() => _session.Dispatch(() =>
    {
        var (window, menu, input, _) = CreateWindow();
        try
        {
            Assert.True(input.Focus());
            input.CaretIndex = input.Text!.Length;
            window.KeyTextInput("!");
            Assert.Equal("alpha beta!", input.Text);
            var undo = Row(menu, TextEditingAction.Undo).Command!;
            var redo = Row(menu, TextEditingAction.Redo).Command!;
            Assert.True(undo.CanExecute(null));
            input.IsReadOnly = true;
            Assert.False(undo.CanExecute(null));
            undo.Execute(null);
            Assert.Equal("alpha beta!", input.Text);
            input.IsReadOnly = false;
            undo.Execute(null);
            Assert.Equal("alpha beta", input.Text);
            Assert.True(redo.CanExecute(null));
            redo.Execute(null);
            Assert.Equal("alpha beta!", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DetachingMenu_DisablesOldCommandsAndUnsubscribesFromEditor() => _session.Dispatch(() =>
    {
        var (window, menu, input, layout) = CreateWindow();
        try
        {
            Assert.True(input.Focus());
            input.SelectAll();
            var command = Row(menu, TextEditingAction.Copy).Command!;
            var notifications = 0;
            command.CanExecuteChanged += (_, _) => notifications++;
            input.SelectionEnd = input.SelectionStart;
            Assert.True(notifications > 0);
            Assert.IsType<AppFlyout>(menu.Flyout).ShowAt(menu);
            layout.Children.Remove(menu);
            var afterDetach = notifications;
            input.SelectAll();
            Assert.False(command.CanExecute(null));
            Assert.False(Assert.IsType<AppFlyout>(menu.Flyout).IsOpen);
            Assert.Equal(afterDetach, notifications);

            layout.Children.Add(menu);
            Assert.True(Row(menu, TextEditingAction.Copy).Command!.CanExecute(null));
            window.Close();
            Assert.False(Row(menu, TextEditingAction.Copy).Command!.CanExecute(null));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task PasswordSelection_CannotCopyOrCutEvenThroughDirectExecution() => _session.DispatchAsync(async () =>
    {
        var (window, menu, input, _) = CreateWindow();
        try
        {
            var clipboard = Assert.IsAssignableFrom<IClipboard>(window.Clipboard);
            await clipboard.SetTextAsync("sentinel");
            Assert.True(input.Focus());
            input.SelectAll();
            input.PasswordChar = '*';
            foreach (var action in new[] { TextEditingAction.Copy, TextEditingAction.Cut })
            {
                var command = Row(menu, action).Command!;
                Assert.False(command.CanExecute(null));
                command.Execute(null);
            }
            Assert.Equal("sentinel", await clipboard.TryGetTextAsync());
            Assert.Equal("alpha beta", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MovingEditorToAnotherWindow_RejectsTheOldTarget() => _session.Dispatch(() =>
    {
        var (window, menu, input, layout) = CreateWindow();
        var other = new AppWindow { Width = 400, Height = 220 };
        try
        {
            Assert.True(input.Focus());
            input.SelectAll();
            var selection = Row(menu, TextEditingAction.SelectAll).Command!;
            Assert.True(selection.CanExecute(null));
            layout.Children.Remove(input);
            other.Content = input;
            other.Show();
            Assert.True(input.Focus());
            input.SelectionEnd = input.SelectionStart;
            Assert.False(selection.CanExecute(null));
            selection.Execute(null);
            Assert.Equal(input.SelectionStart, input.SelectionEnd);
        }
        finally
        {
            other.Close();
            window.Close();
        }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SelectableOutput_OnlyEnablesCopyAndSelection() => _session.Dispatch(() =>
    {
        var (window, menu, _, layout) = CreateWindow();
        try
        {
            var text = new AppSelectableText { Text = "tool result", Focusable = true };
            layout.Children.Add(text);
            Assert.True(text.Focus());
            Row(menu, TextEditingAction.SelectAll).Command!.Execute(null);
            Assert.Equal(text.Text, text.SelectedText);
            Assert.True(Row(menu, TextEditingAction.Copy).Command!.CanExecute(null));
            foreach (var action in new[] { TextEditingAction.Undo, TextEditingAction.Redo, TextEditingAction.Cut, TextEditingAction.Paste })
                Assert.False(Row(menu, action).Command!.CanExecute(null));
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static (AppWindow Window, AppEditMenu Menu, AppInput Input, StackPanel Layout) CreateWindow()
    {
        var input = new AppInput { Text = "alpha beta" };
        var menu = new AppEditMenu();
        var layout = new StackPanel { Children = { menu, input } };
        var window = new AppWindow { Width = 400, Height = 220, Content = layout };
        window.Show();
        return (window, menu, input, layout);
    }

    private static AppActionButton Row(AppEditMenu menu, TextEditingAction action)
    {
        var card = Assert.IsType<AppCard>(Assert.IsType<AppFlyout>(menu.Flyout).Content);
        return Assert.IsType<StackPanel>(card.Child).Children.OfType<AppActionButton>().ElementAt((int)action);
    }
}
