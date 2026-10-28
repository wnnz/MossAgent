using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Application.Plugins;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Domain.Plugins;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AuditSkillActivationTrustPaneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read",
        ToolRiskLevel.ReadOnly, ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 1, 0);

    private static TaskSkillActivation Activation(Guid taskId, Guid pluginId, long trustRevision) =>
        new(Guid.NewGuid(), taskId, pluginId, "demo-plugin", "1.2.0", new string('a', 64),
            "skills/a/SKILL.md", "bounded skill body", DateTimeOffset.UnixEpoch, trustRevision);

    private static InstalledPlugin Plugin(Guid pluginId, long trustRevision) =>
        new(pluginId, "demo-plugin", "1.2.0", new string('a', 64), "demo-plugin-1-2-0",
            InstalledPluginState.Enabled, "/tmp/p.mossplugin", DateTimeOffset.UnixEpoch,
            [], [], PluginOverlaySource.None, trustRevision);

    private static InspectorProbeAuditReader ReaderWithExecution(Guid taskId) => new()
    {
        Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
    };

    private static InspectorProbeSkillActivations StoreWith(TaskSkillActivation activation) => new()
    {
        Reads = (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([activation])
    };

    [Fact]
    public Task CurrentRowWithADifferentRevisionShowsTheTrustChangedMarker() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var plugins = new InspectorProbeInstalledPlugins
        {
            Lookups = (_, _) => Task.FromResult<InstalledPlugin?>(Plugin(pluginId, 4))
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId),
            skillActivations: StoreWith(Activation(taskId, pluginId, 2)),
            installedPlugins: plugins);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Contains(InspectorText.Format("AuditSkillTrustChanged", 2, 4), item.Metadata,
            StringComparison.Ordinal);
        Assert.Equal([pluginId], plugins.Queried);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CurrentRowWithTheSameRevisionShowsNoMarker() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var pluginId = Guid.NewGuid();
        var plugins = new InspectorProbeInstalledPlugins
        {
            Lookups = (_, _) => Task.FromResult<InstalledPlugin?>(Plugin(pluginId, 2))
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId),
            skillActivations: StoreWith(Activation(taskId, pluginId, 2)),
            installedPlugins: plugins);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Contains("· r2", item.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain("信任已变化", item.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingPluginRowShowsTheRemovedMarker() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var plugins = new InspectorProbeInstalledPlugins();
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId),
            skillActivations: StoreWith(Activation(taskId, Guid.NewGuid(), 2)),
            installedPlugins: plugins);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Contains(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task FailedPluginLookupDegradesToTheRemovedMarker() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var plugins = new InspectorProbeInstalledPlugins
        {
            Lookups = (_, _) => Task.FromException<InstalledPlugin?>(new InvalidOperationException("store outage"))
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId),
            skillActivations: StoreWith(Activation(taskId, Guid.NewGuid(), 2)),
            installedPlugins: plugins);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Contains(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
        Assert.NotEqual(InspectorText.Get("AuditFailed"), pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task WithoutAPluginStoreOnlyTheRecordedRevisionShows() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId),
            skillActivations: StoreWith(Activation(taskId, Guid.NewGuid(), 5)));
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Contains("· r5", item.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
        Assert.DoesNotContain("信任已变化", item.Metadata, StringComparison.Ordinal);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompositionRootInjectsTheRegisteredPluginStore() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var plugins = new InspectorProbeInstalledPlugins
        {
            Lookups = (_, _) => Task.FromResult<InstalledPlugin?>(Plugin(Guid.NewGuid(), 9))
        };
        var provider = new ServiceCollection()
            .AddSingleton<IToolExecutionAuditReader>(new InspectorProbeAuditReader())
            .AddSingleton<ITaskSkillActivationStore>(StoreWith(Activation(taskId, Guid.NewGuid(), 2)))
            .AddSingleton<IInstalledPluginStore>(plugins)
            .AddSingleton<AuditPaneViewModel>()
            .BuildServiceProvider();
        using var pane = provider.GetRequiredService<AuditPaneViewModel>();
        pane.Attach(taskId);
        Assert.Single(pane.SkillActivations);
        Assert.Single(plugins.Queried);
    }, TestContext.Current.CancellationToken);
}
