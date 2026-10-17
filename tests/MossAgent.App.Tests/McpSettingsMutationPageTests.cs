using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class McpSettingsMutationPageTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, 980)]
    [InlineData(true, false, 980)]
    [InlineData(false, true, 980)]
    [InlineData(true, true, 980)]
    [InlineData(false, false, 728)]
    [InlineData(true, false, 728)]
    [InlineData(false, true, 728)]
    [InlineData(true, true, 728)]
    public Task Saving_PreservesNewerKeyboardInputAndEscape(bool dark, bool closeEditor, int width) => _session.DispatchAsync(async () =>
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        var pane = new McpSettingsPane { DataContext = fixture.Model };
        var window = new AppWindow
        {
            Width = width,
            Height = 700,
            Content = pane,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            var input = Assert.Single(pane.GetVisualDescendants().OfType<AppInput>(), candidate => candidate.Text == original.Name);
            Type(window, input, "captured-name");
            Assert.Equal("captured-name", fixture.Model.Name);
            var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.Repository.Reader = _ => pending.Task;
            var save = Assert.Single(pane.GetVisualDescendants().OfType<AppButton>(), button => ReferenceEquals(button.Command, fixture.Model.SaveCommand));
            PressSpace(save);
            var run = fixture.Model.SaveCommand.ExecutionTask!;
            Type(window, input, "newer-unsaved-name");
            Assert.Equal("newer-unsaved-name", fixture.Model.Name);
            if (closeEditor) input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
            fixture.Repository.Reader = null;
            pending.SetResult([original]);
            await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Flush(window);
            Assert.Equal("captured-name", Assert.Single(fixture.Repository.Items).Name);
            Assert.Equal("newer-unsaved-name", fixture.Model.Name);
            Assert.Equal(!closeEditor, fixture.Model.IsFormOpen);
            Assert.Equal(original.Id, fixture.Model.SelectedItem?.Id);
            if (!closeEditor) Assert.Equal("newer-unsaved-name", input.Text);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Type(AppWindow window, AppInput input, string text)
    {
        Assert.True(input.Focus());
        input.SelectAll();
        window.KeyTextInput(text);
        Flush(window);
    }

    private static void PressSpace(Control button)
    {
        Assert.True(button.Focus());
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Space });
        button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Space });
    }

    private static void Flush(AppWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
