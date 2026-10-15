using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ComposerSubmissionIntegrationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, false, RawInputModifiers.None)]
    [InlineData(true, true, RawInputModifiers.None)]
    [InlineData(false, true, RawInputModifiers.Control)]
    [InlineData(true, false, RawInputModifiers.Control)]
    public Task FocusedComposerSubmitsOnePersistedMessage(bool dark, bool compact, RawInputModifiers modifiers) =>
        WithWindow(dark, compact, false, async (window, fixture, input) =>
        {
            const string draft = "通过真实输入框提交的合成消息。";
            fixture.Workspace.ComposerText = draft;
            Flush(window);
            Assert.True(input.Focus());
            Press(window, modifiers);
            Assert.True(fixture.Workspace.IsRunning || fixture.Server.Requests.Count > 0,
                "The focused composer must start submission on Enter.");
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Workspace.IsRunning);
            await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
            var task = fixture.Workspace.Catalog.SelectedTask!;
            var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
            Assert.Equal(draft, Assert.Single(messages, message => message.Role == MessageRole.User).Content);
            Assert.Equal(string.Empty, fixture.Workspace.ComposerText);
            Assert.Single(fixture.Server.Requests);
        });

    [Theory]
    [InlineData(RawInputModifiers.None)]
    [InlineData(RawInputModifiers.Control)]
    public Task EnterDuringActiveRunPreservesDraftAndDoesNotStop(RawInputModifiers modifiers) =>
        WithWindow(false, false, true, async (window, fixture, input) =>
        {
            fixture.Workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            fixture.Workspace.ComposerText = "下一条草稿，不应误触停止。";
            Flush(window);
            Assert.True(input.Focus());
            Press(window, modifiers);
            Assert.Equal("下一条草稿，不应误触停止。", fixture.Workspace.ComposerText);
            Assert.True(fixture.Workspace.HasActiveRun);
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => !fixture.Workspace.IsRunning);
            await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
            Assert.Single(fixture.Server.Requests);
        });

    [Fact]
    public Task ShiftEnterInsertsNewlineWithoutSubmitting() =>
        WithWindow(true, true, false, (window, fixture, input) =>
        {
            fixture.Workspace.ComposerText = "第一行";
            Flush(window);
            Assert.True(input.Focus());
            input.CaretIndex = input.Text!.Length;
            Press(window, RawInputModifiers.Shift);
            Assert.Equal("第一行\n", fixture.Workspace.ComposerText.Replace("\r\n", "\n", StringComparison.Ordinal));
            Assert.False(fixture.Workspace.IsRunning);
            Assert.Empty(fixture.Server.Requests);
            return Task.CompletedTask;
        });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ControlEnterOutsideComposerDoesNotSubmit(bool settings) =>
        WithWindow(false, false, false, (window, fixture, input) =>
        {
            const string draft = "只在输入框中发送。";
            fixture.Workspace.ComposerText = draft;
            if (settings)
            {
                fixture.Main.SelectSettingsTabCommand.Execute("5");
                Flush(window);
                var search = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(),
                    candidate => candidate.Classes.Contains("settings-search"));
                Assert.True(search.Focus());
            }
            else
            {
                var header = Assert.Single(window.GetVisualDescendants().OfType<AppConversationHeader>());
                header.Focusable = true;
                Assert.True(header.Focus());
            }
            Press(window, RawInputModifiers.Control);
            Assert.Equal(draft, fixture.Workspace.ComposerText);
            Assert.False(fixture.Workspace.IsRunning);
            Assert.Empty(fixture.Server.Requests);
            return Task.CompletedTask;
        });

    private Task WithWindow(bool dark, bool compact, bool pauseResponse,
        Func<MainWindow, WorkspaceModelSelectionFixture, AppInput, Task> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse);
        var window = new MainWindow
        {
            DataContext = fixture.Main,
            Width = compact ? 526 : 1280,
            Height = compact ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        try
        {
            Flush(window);
            var input = Assert.Single(window.GetVisualDescendants().OfType<AppInput>(),
                candidate => candidate.Classes.Contains("composer-input"));
            await action(window, fixture, input);
        }
        finally { window.Close(); }
    }, TestContext.Current.CancellationToken);

    private static void Press(MainWindow window, RawInputModifiers modifiers)
    {
        window.KeyPress(Key.Enter, modifiers, PhysicalKey.Enter, "\r");
        window.KeyRelease(Key.Enter, modifiers, PhysicalKey.Enter, "\r");
        Flush(window);
    }

    private static void Flush(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
