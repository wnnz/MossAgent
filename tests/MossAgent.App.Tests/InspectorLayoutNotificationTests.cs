using Avalonia;
using Avalonia.Headless;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class InspectorLayoutNotificationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(1000, 1300)]
    [InlineData(1300, 1000)]
    [InlineData(1420, 1600)]
    [InlineData(1600, 1420)]
    public Task WidthChangesWithinTheSameRegion_DoNotInvalidateTheMargin(double first, double second) => WithInspector((inspector, _) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.SetChatViewportWidth(first);
        var margin = inspector.ChatMessageViewportMargin;
        var changes = Observe(inspector);
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            inspector.SetChatViewportWidth(second);
            inspector.SetChatViewportWidth(first);
        }
        Assert.Empty(changes);
        Assert.Equal(margin, inspector.ChatMessageViewportMargin);
    });

    [Theory]
    [InlineData("dismissed")]
    [InlineData("inspector")]
    [InlineData("wide")]
    public Task HiddenOrWideCard_TracksDimensionsWithoutInvalidatingTheMargin(string state) => WithInspector((inspector, _) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.SetChatViewportWidth(state == "wide" ? 1600 : 1000);
        if (state == "dismissed") inspector.ToggleContextCard();
        if (state == "inspector") inspector.IsRightPanelOpen = true;
        var changes = Observe(inspector);
        inspector.SetContextCardHeight(300);
        inspector.SetChatViewportWidth(state == "wide" ? 1800 : 1100);
        Assert.Empty(changes);
        Assert.Equal(default, inspector.ChatMessageViewportMargin);
        if (state == "dismissed") inspector.ToggleContextCard();
        else if (state == "inspector") inspector.IsRightPanelOpen = false;
        else inspector.SetChatViewportWidth(1000);
        Assert.Equal(new Thickness(0, 322, 0, 0), inspector.ChatMessageViewportMargin);
        Assert.Single(changes, name => name == nameof(inspector.ChatMessageViewportMargin));
    });

    [Theory]
    [InlineData(1419, 1420, 0)]
    [InlineData(1420, 1419, 218)]
    public Task CrossingTheOverlapBoundary_PublishesTheNewMarginOnce(double first, double second, double top) => WithInspector((inspector, _) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.SetChatViewportWidth(first);
        var observations = new List<Thickness>();
        inspector.PropertyChanged += (_, args) =>
        {
            Assert.Equal(nameof(inspector.ChatMessageViewportMargin), args.PropertyName);
            observations.Add(inspector.ChatMessageViewportMargin);
        };
        inspector.SetChatViewportWidth(second);
        Assert.Equal(new Thickness(0, top, 0, 0), Assert.Single(observations));
    });

    [Fact]
    public Task WidthBeforeTheFirstHeight_DoesNotInvalidateTheMargin() => WithInspector((inspector, _) =>
    {
        var changes = Observe(inspector);
        inspector.SetChatViewportWidth(1600);
        inspector.SetChatViewportWidth(1000);
        Assert.Empty(changes);
        inspector.SetContextCardHeight(196);
        Assert.Equal([nameof(inspector.ChatMessageViewportMargin)], changes);
        Assert.Equal(new Thickness(0, 218, 0, 0), inspector.ChatMessageViewportMargin);
    });

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-1)]
    public Task InvalidAndSubpixelDimensions_KeepTheLastAcceptedValues(double invalid) => WithInspector((inspector, _) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.SetChatViewportWidth(1000);
        var changes = Observe(inspector);
        inspector.SetContextCardHeight(invalid);
        inspector.SetChatViewportWidth(invalid);
        inspector.SetContextCardHeight(196.25);
        inspector.SetChatViewportWidth(1000.25);
        Assert.Empty(changes);
        Assert.Equal(new Thickness(0, 218, 0, 0), inspector.ChatMessageViewportMargin);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task TogglingTheCard_DoesNotInvalidateThePanelLabel(bool measured) => WithInspector((inspector, _) =>
    {
        if (measured) inspector.SetContextCardHeight(196);
        var changes = Observe(inspector);
        inspector.ToggleContextCard();
        Assert.Equal(measured
            ? [nameof(inspector.IsContextCardVisible), nameof(inspector.ChatMessageViewportMargin)]
            : new[] { nameof(inspector.IsContextCardVisible) }, changes);
        changes.Clear();
        inspector.ToggleContextCard();
        Assert.Equal(measured
            ? [nameof(inspector.IsContextCardVisible), nameof(inspector.ChatMessageViewportMargin)]
            : new[] { nameof(inspector.IsContextCardVisible) }, changes);
    });

    [Fact]
    public Task TogglingThePanelWithADismissedCard_OnlyInvalidatesThePanelAndLabel() => WithInspector((inspector, _) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.ToggleContextCard();
        var changes = Observe(inspector);
        inspector.ToggleRightPanel();
        Assert.Equal([nameof(inspector.IsRightPanelOpen), nameof(inspector.RightPanelToggleLabel)], changes);
        changes.Clear();
        inspector.ToggleRightPanel();
        Assert.Equal([nameof(inspector.IsRightPanelOpen), nameof(inspector.RightPanelToggleLabel)], changes);
        Assert.False(inspector.IsContextCardVisible);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task IdenticalPresentationInAnotherTask_DoesNotPublishRedundantProperties(bool customized) => WithInspector((inspector, catalog) =>
    {
        var first = AddTask(catalog);
        if (customized) { inspector.ActiveInspectorTab = "browser"; inspector.IsRightPanelOpen = true; }
        AddTask(catalog);
        if (customized) { inspector.ActiveInspectorTab = "browser"; inspector.IsRightPanelOpen = true; }
        var changes = Observe(inspector);
        catalog.SelectTaskCommand.Execute(first);
        Assert.Empty(changes);
        Assert.Equal(customized ? "browser" : "home", inspector.ActiveInspectorTab);
        Assert.Equal(customized, inspector.IsRightPanelOpen);
    });

    [Fact]
    public Task ChangedTaskPresentation_IsCompleteBeforeAnyNotification() => WithInspector((inspector, catalog) =>
    {
        inspector.SetContextCardHeight(196);
        var first = AddTask(catalog);
        inspector.ActiveInspectorTab = "browser";
        inspector.IsRightPanelOpen = true;
        AddTask(catalog);
        var changes = Observe(inspector);
        inspector.PropertyChanged += (_, _) =>
        {
            Assert.Equal("browser", inspector.ActiveInspectorTab);
            Assert.True(inspector.IsRightPanelOpen);
            Assert.False(inspector.IsContextCardVisible);
            Assert.Equal(default, inspector.ChatMessageViewportMargin);
        };
        catalog.SelectTaskCommand.Execute(first);
        Assert.Contains(nameof(inspector.ActiveInspectorTab), changes);
        Assert.Contains(nameof(inspector.IsRightPanelOpen), changes);
        Assert.Contains(nameof(inspector.IsContextCardVisible), changes);
        Assert.Contains(nameof(inspector.ChatMessageViewportMargin), changes);
    });

    [Fact]
    public Task DisposedInspector_IgnoresMeasurementsCommandsAndLaterSelection() => WithInspector((inspector, catalog) =>
    {
        inspector.SetContextCardHeight(196);
        inspector.ActiveInspectorTab = "browser";
        inspector.Dispose();
        var changes = Observe(inspector);
        inspector.SetContextCardHeight(300);
        inspector.SetChatViewportWidth(1600);
        inspector.ToggleContextCard();
        inspector.ToggleRightPanel();
        inspector.ActiveInspectorTab = "audit";
        AddTask(catalog);
        Assert.Empty(changes);
        Assert.Equal("browser", inspector.ActiveInspectorTab);
        Assert.Equal(new Thickness(0, 218, 0, 0), inspector.ChatMessageViewportMargin);
    });

    private Task WithInspector(Action<WorkspaceInspectorViewModel, WorkspaceCatalogViewModel> action) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        using var inspector = new WorkspaceInspectorViewModel(fixture.Workspace.Catalog);
        action(inspector, fixture.Workspace.Catalog);
        Assert.Empty(fixture.Server.Requests);
    }, TestContext.Current.CancellationToken);

    private static List<string?> Observe(WorkspaceInspectorViewModel inspector)
    {
        var changes = new List<string?>();
        inspector.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        return changes;
    }

    private static AgentTask AddTask(WorkspaceCatalogViewModel catalog)
    {
        var now = DateTimeOffset.UtcNow;
        var task = new AgentTask(Guid.NewGuid(), catalog.SelectedProject!.Id, "Synthetic layout task",
            ApprovalPolicy.ReadOnly, AgentTaskStatus.Completed, now, now, null);
        catalog.UpsertTask(task);
        return task;
    }
}
