using Avalonia.Headless;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.Resources;
using MossAgent.App.Views;
using MossAgent.Application.Settings;
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

    [Fact]
    public Task EnterDuringActiveRunQueuesTheMessageWithoutStoppingAndPreservesTheNextDraft() =>
        WithWindow(false, false, true, async (window, fixture, input) =>
        {
            fixture.Workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            fixture.Workspace.ComposerText = "下一条草稿，不应误触停止。";
            Flush(window);
            Assert.True(input.Focus());
            Press(window, RawInputModifiers.None);
            await fixture.WaitUntilAsync(() => fixture.Workspace.Queue.Items.Count == 1 && !fixture.Workspace.Queue.IsBusy);
            Assert.Equal("下一条草稿，不应误触停止。", Assert.Single(fixture.Workspace.Queue.Items).Prompt);
            Assert.Equal(string.Empty, fixture.Workspace.ComposerText);
            Assert.True(fixture.Workspace.HasActiveRun);
            fixture.Workspace.ComposerText = "队列提交之后的未发送草稿。";
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !fixture.Workspace.IsRunning);
            await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
            Assert.Equal("队列提交之后的未发送草稿。", fixture.Workspace.ComposerText);
            Assert.Empty(fixture.Workspace.Queue.Items);
        });

    [Fact]
    public Task ControlEnterDuringActiveRunSteersTheSameRunAndPreservesTheNextDraft() =>
        WithWindow(true, true, true, async (window, fixture, input) =>
        {
            var workspace = fixture.Workspace;
            workspace.PrimaryActionCommand.Execute(null);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
            var taskId = workspace.Catalog.SelectedTask!.Id;
            Assert.True(fixture.Runs.TryGet(taskId, out var original));
            var runId = original!.Steering!.RunId;
            const string guidance = "继续当前运行，不取消也不加入队列。";
            workspace.ComposerText = guidance;
            Flush(window);
            Assert.True(input.Focus());

            Press(window, RawInputModifiers.Control);

            await fixture.WaitUntilAsync(() => workspace.Steering.History.Items.Count == 1 && !workspace.Steering.IsBusy);
            var accepted = Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
            Assert.Equal(guidance, accepted.Text);
            Assert.Equal(runId, accepted.RunId);
            Assert.Equal(ConversationSteeringInputState.Pending, accepted.State);
            Assert.True(fixture.Runs.TryGet(taskId, out var current));
            Assert.Same(original, current);
            Assert.False(original.IsCancellationRequested);
            Assert.Empty(workspace.Queue.Items);
            Assert.Equal(string.Empty, workspace.ComposerText);
            workspace.ComposerText = "引导提交之后的未发送草稿。";
            fixture.Server.ReleaseResponses();
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
            await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
            Assert.Equal(taskId, workspace.Catalog.SelectedTask!.Id);
            Assert.Equal("引导提交之后的未发送草稿。", workspace.ComposerText);
            Assert.Contains(fixture.Server.Requests[1].GetProperty("input").EnumerateArray(),
                item => item.TryGetProperty("content", out var content) && content.ValueKind == System.Text.Json.JsonValueKind.String
                    && content.GetString() == guidance);
            var applied = Assert.Single((await fixture.Steering.GetInputsAsync(taskId, long.MaxValue, 64, fixture.Token)).Items);
            Assert.Equal(ConversationSteeringInputState.AppliedAtBoundary, applied.State);
            Assert.Equal(accepted.Id, applied.Id);
            Assert.Single(await fixture.Tasks.GetMessagesAsync(taskId, fixture.Token), message => message.Id == accepted.Id);
            Assert.Empty(workspace.Queue.Items);
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

    [Fact]
    public Task PersistedControlEnterModeMakesPlainEnterInsertANewlineAndControlEnterSubmit() =>
        WithWindow(false, false, false, async (window, fixture, input) =>
        {
            Assert.False(fixture.Workspace.ComposerPreferences.SubmitOnEnter);
            fixture.Workspace.ComposerText = "Ctrl+Enter 模式提交";
            Flush(window);
            Assert.True(input.Focus());

            Press(window, RawInputModifiers.None);
            Assert.Empty(fixture.Server.Requests);
            Assert.Contains('\n', fixture.Workspace.ComposerText);

            Press(window, RawInputModifiers.Control);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Workspace.IsRunning);
            await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
            Assert.Single(fixture.Server.Requests);
        }, services => services.AddSingleton<IComposerPreferenceStore>(
            new TestComposerPreferenceStore { Mode = ComposerSendMode.ControlEnterSends }));

    [Fact]
    public Task SettingsSelectionPersistsAndImmediatelyChangesKeyboardSubmission()
    {
        var store = new TestComposerPreferenceStore();
        return WithWindow(false, false, false, async (window, fixture, input) =>
        {
            fixture.Main.SelectSettingsTabCommand.Execute("5");
            Flush(window);
            var selector = Assert.Single(window.GetVisualDescendants().OfType<AppComboBox>(),
                candidate => AutomationProperties.GetName(candidate) == ComposerInputText.SettingsTitle);
            selector.SelectedIndex = 1;
            Flush(window);

            await fixture.WaitUntilAsync(() => store.Mode == ComposerSendMode.ControlEnterSends
                && !fixture.Workspace.ComposerPreferences.HasError);
            fixture.Main.CloseSettingsCommand.Execute(null);
            fixture.Workspace.ComposerText = "设置页切换后由 Ctrl+Enter 发送";
            Flush(window);
            Assert.True(input.Focus());
            Press(window, RawInputModifiers.None);
            Assert.Empty(fixture.Server.Requests);
            Assert.Contains('\n', fixture.Workspace.ComposerText);

            Press(window, RawInputModifiers.Control);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1);
        }, services => services.AddSingleton<IComposerPreferenceStore>(store));
    }

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
        Func<MainWindow, WorkspaceModelSelectionFixture, AppInput, Task> action,
        Action<IServiceCollection>? configure = null) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse, configure: configure);
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
