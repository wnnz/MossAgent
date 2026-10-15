using Avalonia.Controls;
using Avalonia.Headless;
using MossAgent.App.Controls;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EditMenuSelectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NativeLostFocus_PreservesForwardAndReverseSelection(bool reverse) => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "alpha beta", ClearSelectionOnLostFocus = true };
        var menu = new AppEditMenu();
        var window = new AppWindow { Width = 400, Height = 240, Content = new StackPanel { Children = { menu, input } } };
        window.Show();
        try
        {
            Assert.True(input.Focus());
            input.SelectionStart = reverse ? 5 : 0;
            input.SelectionEnd = reverse ? 0 : 5;
            Assert.True(menu.Focus());
            Assert.IsType<AppFlyout>(menu.Flyout).ShowAt(menu);
            Assert.Equal(reverse ? 5 : 0, input.SelectionStart);
            Assert.Equal(reverse ? 0 : 5, input.SelectionEnd);
            var rows = Assert.IsType<StackPanel>(Assert.IsType<AppCard>(Assert.IsType<AppFlyout>(menu.Flyout).Content).Child)
                .Children.OfType<AppActionButton>().ToArray();
            Assert.True(rows[(int)TextEditingAction.Copy].Command!.CanExecute(null));
            Assert.True(rows[(int)TextEditingAction.Cut].Command!.CanExecute(null));
            Assert.True(input.ClearSelectionOnLostFocus);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task InactiveTextReplacement_DoesNotRestoreSelectionOntoDifferentContent() => _session.Dispatch(() =>
    {
        var input = new AppInput { Text = "alpha beta", ClearSelectionOnLostFocus = true };
        var menu = new AppEditMenu();
        var window = new AppWindow { Width = 400, Height = 240, Content = new StackPanel { Children = { menu, input } } };
        window.Show();
        try
        {
            Assert.True(input.Focus());
            input.SelectAll();
            Assert.True(menu.Focus());
            input.Text = "replacement";
            input.SelectionStart = 0;
            input.SelectionEnd = 0;
            Assert.IsType<AppFlyout>(menu.Flyout).ShowAt(menu);
            Assert.Equal(input.SelectionStart, input.SelectionEnd);
            var copy = Assert.IsType<StackPanel>(Assert.IsType<AppCard>(Assert.IsType<AppFlyout>(menu.Flyout).Content).Child)
                .Children.OfType<AppActionButton>().ElementAt((int)TextEditingAction.Copy);
            Assert.False(copy.Command!.CanExecute(null));
            Assert.Equal("replacement", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);
}
