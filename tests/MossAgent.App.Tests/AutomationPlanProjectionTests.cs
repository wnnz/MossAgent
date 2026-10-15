using Avalonia.Headless;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AutomationPlanProjectionTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task ManyPlans_ScanProjectMetadataOnceAndKeepDuplicateOrder() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var metadata = Enumerable.Range(0, 63).Select(index => project with { Id = Guid.NewGuid(), Name = $"project-{index}" })
            .Append(project).ToArray();
        var counted = new CountingReadOnlyList<ProjectProfile>(metadata);
        var schedule = AutomationListOwnershipTests.Schedule(fixture);
        var schedules = Enumerable.Range(0, 32).Select(index => schedule with { Name = $"plan-{index}" }).ToArray();
        using var list = CreateList(fixture, schedules, new TestWorkspaceRepository { ProjectItems = counted });

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        Assert.Equal(metadata.Length, counted.ItemReads);
        Assert.Equal(schedules, list.Items.Select(item => item.Schedule));
        Assert.All(list.Items, item => Assert.Equal(project.Name, item.ProjectLabel));
        Assert.NotSame(list.Items[0], list.Items[1]);
        Assert.Same(list.Items[0], list.SelectedItem);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DuplicateCatalogKeys_KeepFirstMatchAndProviderOwnership() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var (configurations, provider, model) = ModelPickerReloadTests.Profiles();
        var secondProvider = provider with { Id = Guid.NewGuid(), Name = "second-provider" };
        var secondModel = model with { ProviderId = secondProvider.Id, DisplayName = "second-model" };
        configurations.Providers.Add(provider with { Name = "ignored-provider" });
        configurations.Providers.Add(secondProvider);
        configurations.Models.Add(model with { DisplayName = "ignored-model" });
        configurations.Models.Add(secondModel);
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var schedule = AutomationListOwnershipTests.Schedule(fixture) with { ProviderId = provider.Id, ModelId = model.Id };
        AutomationSchedule[] schedules = [schedule, schedule with { ProviderId = secondProvider.Id }, schedule];
        using var list = CreateList(fixture, schedules, new TestWorkspaceRepository
        { ProjectItems = [project, project with { Name = "ignored-project" }] }, configurations);

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        Assert.Equal(schedules, list.Items.Select(item => item.Schedule));
        Assert.All(list.Items, item => Assert.Equal(project.Name, item.ProjectLabel));
        Assert.Equal(new ModelPickerItemViewModel(provider, model).AccessibleName, list.Items[0].ModelLabel);
        Assert.Equal(new ModelPickerItemViewModel(secondProvider, secondModel).AccessibleName, list.Items[1].ModelLabel);
        Assert.Equal(list.Items[0].ModelLabel, list.Items[2].ModelLabel);
        Assert.NotSame(list.Items[0], list.Items[2]);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("project")]
    [InlineData("model")]
    [InlineData("provider")]
    [InlineData("disabled-model")]
    [InlineData("disabled-provider")]
    public Task UnavailableMetadata_DoesNotFallBackToAnotherIdentity(string reason) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var (configurations, provider, model) = ModelPickerReloadTests.Profiles();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var schedule = AutomationListOwnershipTests.Schedule(fixture) with { ProviderId = provider.Id, ModelId = model.Id };
        schedule = reason switch
        {
            "project" => schedule with { ProjectId = Guid.NewGuid() },
            "model" => schedule with { ModelId = Guid.NewGuid() },
            "provider" => schedule with { ProviderId = Guid.NewGuid() },
            _ => schedule
        };
        if (reason == "disabled-model") configurations.Models[0] = model with { IsEnabled = false };
        if (reason == "disabled-provider") configurations.Providers[0] = provider with { IsEnabled = false };
        using var list = CreateList(fixture, [schedule], new TestWorkspaceRepository { ProjectItems = [project] }, configurations);

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        var row = Assert.Single(list.Items);
        Assert.Same(schedule, row.Schedule);
        Assert.Equal(reason == "project" ? AutomationText.Get("Unavailable") : project.Name, row.ProjectLabel);
        Assert.Equal(reason == "project" ? new ModelPickerItemViewModel(provider, model).AccessibleName
            : AutomationText.Get("Unavailable"), row.ModelLabel);
        Assert.False(row.IsRunning);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task Reload_UsesFreshMetadataWithoutChangingPreviouslyPublishedRows() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var (configurations, provider, model) = ModelPickerReloadTests.Profiles();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var projects = new TestWorkspaceRepository { ProjectItems = [project] };
        var schedule = AutomationListOwnershipTests.Schedule(fixture) with { ProviderId = provider.Id, ModelId = model.Id };
        using var list = CreateList(fixture, [schedule], projects, configurations);
        await list.ReloadAsync();
        var previous = Assert.Single(list.Items);
        projects.ProjectItems = [project with { Name = "renamed-project" }];
        configurations.Models[0] = model with { DisplayName = "renamed-model" };

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        var current = Assert.Single(list.Items);
        Assert.NotSame(previous, current);
        Assert.Equal(project.Name, previous.ProjectLabel);
        Assert.Equal(new ModelPickerItemViewModel(provider, model).AccessibleName, previous.ModelLabel);
        Assert.Equal("renamed-project", current.ProjectLabel);
        Assert.Equal("renamed-model · fixture", current.ModelLabel);
        Assert.Same(current, list.SelectedItem);
        Assert.False(list.Owns(previous));
        Assert.Equal(2, list.SnapshotVersion);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task EmptyPlans_DoNotScanProjectMetadata() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var counted = new CountingReadOnlyList<ProjectProfile>([fixture.Workspace.Catalog.SelectedProject!]);
        using var list = CreateList(fixture, [], new TestWorkspaceRepository { ProjectItems = counted });

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        Assert.True(list.IsEmpty);
        Assert.Equal(0, counted.ItemReads);
        Assert.Null(list.SelectedItem);
    }, TestContext.Current.CancellationToken);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public Task MissingOrEmptyFirstProjectName_DoesNotSelectALaterDuplicate(string? name) => _session.DispatchAsync(async () =>
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        using var list = CreateList(fixture, [AutomationListOwnershipTests.Schedule(fixture)], new TestWorkspaceRepository
        { ProjectItems = [project with { Name = name! }, project with { Name = "ignored-project" }] });

        await list.ReloadAsync();

        Assert.False(list.HasLoadError, list.LoadError);
        Assert.Equal(name ?? AutomationText.Get("Unavailable"), Assert.Single(list.Items).ProjectLabel);
    }, TestContext.Current.CancellationToken);

    private static AutomationPlanListViewModel CreateList(WorkspaceModelSelectionFixture fixture,
        IReadOnlyList<AutomationSchedule> schedules, IWorkspaceRepository projects, IConfigurationRepository? configurations = null) =>
        new(new AutomationListProbeRepository(fixture.Automations) { Read = _ => Task.FromResult(schedules) }, projects,
            new EnabledModelCatalog(configurations ?? fixture.Configurations), fixture.Scheduler);
}
