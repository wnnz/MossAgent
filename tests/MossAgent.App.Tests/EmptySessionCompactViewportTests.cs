using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class EmptySessionCompactViewportTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(526, 601, false, false, false)]
    [InlineData(526, 601, true, false, false)]
    [InlineData(526, 601, false, true, false)]
    [InlineData(526, 601, true, true, false)]
    [InlineData(980, 640, false, false, false)]
    [InlineData(980, 640, true, false, false)]
    [InlineData(980, 640, false, true, false)]
    [InlineData(980, 640, true, true, false)]
    [InlineData(526, 601, false, false, true)]
    [InlineData(526, 601, true, false, true)]
    [InlineData(526, 601, false, true, true)]
    [InlineData(526, 601, true, true, true)]
    public Task SavedEmptyConversation_HeadingDoesNotOverlapContextOrComposer(int width, int height, bool dark, bool longName, bool withAgent) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var project = fixture.Workspace.Catalog.SelectedProject! with
            {
                Name = longName ? new string('项', 80) : "并发项目 A"
            };
            await fixture.Tasks.SaveProjectAsync(project, fixture.Token);
            var task = new AgentTask(Guid.NewGuid(), project.Id, "Empty viewport fixture", ApprovalPolicy.ReadOnly,
                AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
            await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
            await fixture.Workspace.Catalog.ReloadAsync();
            fixture.Workspace.Catalog.SelectTaskCommand.Execute(task);
            fixture.Workspace.ComposerText = string.Empty;
            if (withAgent) await AddAgentAsync(fixture);
            var window = new MainWindow
            {
                DataContext = fixture.Main,
                WindowState = WindowState.Normal,
                Width = width,
                Height = height,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                Settle(window);
                Assert.True(fixture.Workspace.IsEmptySession);
                Assert.True(fixture.Main.IsContextCardVisible);
                Assert.True(fixture.Main.ChatMessageViewportMargin.Top > 0);
                var prompt = Assert.Single(window.GetVisualDescendants().OfType<AppProjectPrompt>(), control => control.IsEffectivelyVisible);
                var composer = Assert.Single(window.GetVisualDescendants().OfType<WorkspaceComposerView>(), control => control.IsEffectivelyVisible);
                var context = Assert.Single(window.GetVisualDescendants().OfType<AppCard>(),
                    control => control.IsEffectivelyVisible && control.Classes.Contains("inspector-surface"));
                var brand = Assert.Single(window.GetVisualDescendants().OfType<AppIcon>(),
                    control => control.IsEffectivelyVisible && control.Classes.Contains("empty-agent-offset"));
                var headings = prompt.GetVisualDescendants().Where(control => control is AppText or AppButton).OfType<Control>();
                foreach (var control in headings.Append(brand))
                {
                    var bounds = Bounds(control, window);
                    Assert.True(bounds.Top >= Bounds(context, window).Bottom,
                        $"Heading {bounds} overlaps context {Bounds(context, window)}");
                    Assert.True(bounds.Bottom <= Bounds(composer, window).Top,
                        $"Heading {bounds} overlaps composer {Bounds(composer, window)}");
                    Assert.True(bounds.Left >= 52 && bounds.Right <= window.ClientSize.Width - 4);
                }
                Assert.Empty(fixture.Server.Requests);
                Assert.Equal(task.Id, fixture.Workspace.Catalog.SelectedTask?.Id);
                SaveEvidence(window, $"empty-dark-{dark}-width-{width}-long-{longName}-agent-{withAgent}");
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);

    private static async Task AddAgentAsync(WorkspaceModelSelectionFixture fixture)
    {
        var agents = fixture.Main.Agents;
        await agents.RefreshCommand.ExecuteAsync(null);
        agents.NewCommand.Execute(null);
        agents.Name = "有代理的窄窗口验收";
        agents.SystemPrompt = "synthetic prompt";
        agents.SelectedModel = Assert.Single(agents.Models, option => option.Model.Id == fixture.SelectedModel.Id);
        await agents.SaveCommand.ExecuteAsync(null);
        Assert.False(agents.HasStatusError, agents.Status);
        await fixture.Workspace.ReloadAgentsAsync();
        fixture.Workspace.SelectAgentCommand.Execute(Assert.Single(fixture.Workspace.Agents));
    }

    private static void SaveEvidence(AppWindow window, string name)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_PREPARATION_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        using var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(directory, $"{name}.png"), PngBitmapEncoderOptions.Default);
    }

    private static void Settle(MainWindow window)
    {
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }

    private static Rect Bounds(Control control, Control root) =>
        new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);
}
