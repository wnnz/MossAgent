using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class WorkspaceAgentComposerStateTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(false, 360, "loading")]
    [InlineData(true, 360, "loading")]
    [InlineData(false, 740, "loading")]
    [InlineData(true, 740, "loading")]
    [InlineData(false, 360, "error")]
    [InlineData(true, 360, "error")]
    [InlineData(false, 740, "error")]
    [InlineData(true, 740, "error")]
    [InlineData(false, 360, "unavailable")]
    [InlineData(true, 360, "unavailable")]
    [InlineData(false, 740, "ready")]
    [InlineData(true, 740, "ready")]
    public Task SharedComposer_PresentsTheReasonAndKeepsTheDraftWithoutOverflow(bool dark, int width, string state) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
            var repository = new TestAgentConfigurationRepository();
            var profile = new AgentProfile(Guid.NewGuid(), "用于验证不可用代理仍然显示而不是静默回退的长名称",
                "synthetic", fixture.AlternativeModel.ProviderId, fixture.AlternativeModel.Id, "high", true);
            await repository.SaveAsync(profile, fixture.Token);
            using var workspace = fixture.CreateWorkspace(fixture.Tasks, fixture.Runs, repository);
            await workspace.LoadAsync();
            workspace.SelectAgentCommand.Execute(profile);
            workspace.ComposerText = "retained-draft";
            var gate = new TaskCompletionSource<IReadOnlyList<AgentProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
            Task? pending = null;
            if (state == "loading")
            {
                repository.Loader = _ => gate.Task;
                pending = workspace.ReloadAgentsAsync();
            }
            else if (state == "error")
            {
                repository.Loader = _ => throw new InvalidOperationException("fixture-secret-error");
                await workspace.ReloadAgentsAsync();
            }
            else if (state == "unavailable")
            {
                await repository.SaveAsync(profile with { IsEnabled = false }, fixture.Token);
                await workspace.ReloadAgentsAsync();
            }
            var view = new WorkspaceComposerView { DataContext = workspace };
            var window = new AppWindow
            {
                Content = view,
                Width = width,
                Height = 640,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            window.Show();
            try
            {
                window.UpdateLayout();
                AssertComposerState(view, workspace, state);
                Assert.Empty(fixture.Server.Requests);
            }
            finally
            {
                gate.TrySetResult([profile]);
                if (pending is not null) await pending;
                window.Close();
            }
        }, TestContext.Current.CancellationToken);

    private static void AssertComposerState(WorkspaceComposerView view, WorkspaceViewModel workspace, string state)
    {
        var bar = Assert.Single(view.GetVisualDescendants().OfType<AppComposerContextBar>());
        var agent = bar.FindControl<AppActionButton>("AgentButton")!;
        var send = Assert.Single(view.GetVisualDescendants().OfType<AppIconButton>(),
            button => button.Name == "SendButton");
        var notice = Assert.Single(view.GetVisualDescendants().OfType<AppText>(),
            text => text.Text == workspace.AgentPicker.CatalogNotice);
        Assert.True(agent.IsVisible);
        Assert.Equal(state is "ready" or "unavailable", agent.IsEffectivelyEnabled);
        Assert.Equal(state == "ready", send.IsEffectivelyEnabled);
        Assert.Equal(state != "ready", notice.GetVisualAncestors().OfType<Grid>().First().IsVisible);
        Assert.Equal("retained-draft", workspace.ComposerText);
        Assert.DoesNotContain("fixture-secret-error", notice.Text!, StringComparison.Ordinal);
        if (state == "ready") return;
        var location = notice.TranslatePoint(default, view)!.Value;
        Assert.True(notice.Bounds.Width > 0 && notice.Bounds.Height > 0);
        Assert.InRange(location.X + notice.Bounds.Width, 0, view.Bounds.Width + 0.5);
        Assert.InRange(location.Y + notice.Bounds.Height, 0, view.Bounds.Height + 0.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task LargeAgentMenu_IsBoundedAndStillOffersTheLastConfiguredAgent(bool dark) => _session.DispatchAsync(async () =>
    {
        using var fixture = await WorkspaceAgentPickerTestFixture.CreateAsync();
        for (var index = 0; index < 28; index++)
            await fixture.Repository.SaveAsync(fixture.First with { Id = Guid.NewGuid(), Name = $"agent-{index:00}" },
                TestContext.Current.CancellationToken);
        await fixture.Picker.ReloadAsync(TestContext.Current.CancellationToken);
        var bar = new AppComposerContextBar
        {
            HasAgents = true,
            Agents = fixture.Picker.Items,
            AgentName = "默认代理",
            SelectAgentCommand = fixture.Picker.SelectCommand
        };
        var window = new AppWindow
        {
            Content = bar,
            Width = 360,
            Height = 640,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        window.Show();
        var button = bar.FindControl<AppActionButton>("AgentButton")!;
        var flyout = Assert.IsType<AppFlyout>(button.Flyout);
        try
        {
            flyout.ShowAt(button);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var card = Assert.IsType<AppCard>(flyout.Content);
            var scroll = Assert.Single(card.GetVisualDescendants().OfType<AppScrollViewer>());
            Assert.Equal(240, scroll.MaxHeight);
            Assert.InRange(scroll.Bounds.Height, 1, 240);
            Assert.True(scroll.Extent.Height > scroll.Viewport.Height);
            var last = fixture.Picker.Items[^1];
            var action = Assert.Single(card.GetVisualDescendants().OfType<AppActionButton>(),
                candidate => ReferenceEquals(candidate.CommandParameter, last));
            Assert.True(action.IsEffectivelyEnabled);
            action.Command!.Execute(action.CommandParameter);
            Assert.Same(last, fixture.Picker.SelectedProfile);
        }
        finally { flyout.Hide(); window.Close(); }
    }, TestContext.Current.CancellationToken);
}
