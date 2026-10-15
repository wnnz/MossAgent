using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorTabNotificationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    public static TheoryData<string, string> Transitions
    {
        get
        {
            string[] tabs = ["home", "sources", "diff", "terminal", "browser", "audit"];
            var transitions = new TheoryData<string, string>();
            foreach (var previous in tabs)
                foreach (var current in tabs) transitions.Add(previous, current);
            return transitions;
        }
    }

    [Theory]
    [MemberData(nameof(Transitions))]
    public Task DirectSelection_PublishesOnlyChangedTabProperties(string previous, string current) => WithInspector((inspector, _) =>
    {
        inspector.ActiveInspectorTab = previous;
        AssertOnlyChanges(inspector, Expected(current), () => inspector.ActiveInspectorTab = current);
    });

    [Theory]
    [MemberData(nameof(Transitions))]
    public Task RestoringAnotherTask_PublishesOnlyChangedTabProperties(string previous, string current) => WithInspector((inspector, catalog) =>
    {
        var restored = AddTask(catalog);
        inspector.ActiveInspectorTab = current;
        AddTask(catalog);
        inspector.ActiveInspectorTab = previous;
        AssertOnlyChanges(inspector, Expected(current), () => catalog.SelectTaskCommand.Execute(restored));
    });

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("BROWSER")]
    [InlineData(" browser ")]
    [InlineData("unknown")]
    public Task InvalidTabs_KeepThePublishedStateWithoutNotifications(string? invalid) => WithInspector((inspector, _) =>
    {
        inspector.ActiveInspectorTab = "browser";
        AssertOnlyChanges(inspector, Expected("browser"), () => inspector.ActiveInspectorTab = invalid!);
    });

    private static void AssertOnlyChanges(WorkspaceInspectorViewModel inspector,
        Dictionary<string, object> expected, Action change)
    {
        var previous = Snapshot(inspector);
        var expectedChanges = expected.Keys.Where(name => !Equals(previous[name], expected[name]));
        var changes = new List<string>();
        void HandleChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            changes.Add(Assert.IsType<string>(args.PropertyName));
            Assert.Equal(expected, Snapshot(inspector));
        }
        inspector.PropertyChanged += HandleChanged;
        try
        {
            change();
            Assert.Equal(expected, Snapshot(inspector));
            Assert.Equal(expectedChanges.Order(StringComparer.Ordinal), changes.Order(StringComparer.Ordinal));
        }
        finally { inspector.PropertyChanged -= HandleChanged; }
    }

    private static Dictionary<string, object> Snapshot(WorkspaceInspectorViewModel inspector) => new()
    {
        [nameof(inspector.ActiveInspectorTab)] = inspector.ActiveInspectorTab,
        [nameof(inspector.InspectorTitle)] = inspector.InspectorTitle,
        [nameof(inspector.IsInspectorHome)] = inspector.IsInspectorHome,
        [nameof(inspector.IsInspectorDiff)] = inspector.IsInspectorDiff,
        [nameof(inspector.IsInspectorTerminal)] = inspector.IsInspectorTerminal,
        [nameof(inspector.IsInspectorBrowser)] = inspector.IsInspectorBrowser,
        [nameof(inspector.IsInspectorAudit)] = inspector.IsInspectorAudit,
        [nameof(inspector.IsInspectorSources)] = inspector.IsInspectorSources
    };

    private static Dictionary<string, object> Expected(string tab) => new()
    {
        [nameof(WorkspaceInspectorViewModel.ActiveInspectorTab)] = tab,
        [nameof(WorkspaceInspectorViewModel.InspectorTitle)] = InspectorText.TabTitle(tab),
        [nameof(WorkspaceInspectorViewModel.IsInspectorHome)] = tab == "home",
        [nameof(WorkspaceInspectorViewModel.IsInspectorDiff)] = tab == "diff",
        [nameof(WorkspaceInspectorViewModel.IsInspectorTerminal)] = tab == "terminal",
        [nameof(WorkspaceInspectorViewModel.IsInspectorBrowser)] = tab == "browser",
        [nameof(WorkspaceInspectorViewModel.IsInspectorAudit)] = tab == "audit",
        [nameof(WorkspaceInspectorViewModel.IsInspectorSources)] = tab is "home" or "sources"
    };

    private Task WithInspector(Action<WorkspaceInspectorViewModel, WorkspaceCatalogViewModel> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        using var inspector = new WorkspaceInspectorViewModel(fixture.Workspace.Catalog);
        action(inspector, fixture.Workspace.Catalog);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    private static AgentTask AddTask(WorkspaceCatalogViewModel catalog)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), catalog.SelectedProject!.Id, "Synthetic tab task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        catalog.UpsertTask(task);
        return task;
    }
}
