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
public sealed class AuditSkillActivationPaneTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    private static ToolExecutionAuditRecord Record(Guid taskId) => new(Guid.NewGuid(), taskId, "call", "file.read",
        ToolRiskLevel.ReadOnly, ApprovalPolicy.ReadOnly, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, true, true, null, 1, 0);

    private static TaskSkillActivation Activation(Guid taskId, string? text = null,
        long trustRevision = 1) => new(Guid.NewGuid(), taskId,
        Guid.NewGuid(), "demo-plugin", "1.2.0", new string('a', 64), "skills/a/SKILL.md",
        text ?? "bounded skill body", DateTimeOffset.UnixEpoch, trustRevision);

    private static InspectorProbeAuditReader ReaderWithExecution(Guid taskId) => new()
    {
        Recent = (_, _) => Task.FromResult<IReadOnlyList<ToolExecutionAuditRecord>>([Record(taskId)])
    };

    [Fact]
    public Task AttachLoadsPersistedActivationsBesideExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var activation = Activation(taskId);
        var store = new InspectorProbeSkillActivations
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([activation])
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId), skillActivations: store);
        pane.Attach(taskId);
        var item = Assert.Single(pane.SkillActivations);
        Assert.Equal(activation.Id, item.Record.Id);
        Assert.Equal("demo-plugin v1.2.0", item.ProfileName);
        Assert.Equal("success", item.OutcomeKind);
        Assert.Equal(InspectorText.Get("AuditSkillInjected"), item.OutcomeLabel);
        Assert.Contains("skills/a/SKILL.md", item.Metadata, StringComparison.Ordinal);
        Assert.Contains("aaaaaaaa", item.Metadata, StringComparison.Ordinal);
        Assert.True(pane.HasSkillActivations);
        Assert.Equal(InspectorText.Format("AuditSkillActivationsTitle", 1), pane.SkillActivationsTitle);
        Assert.Same(item, pane.SelectedSkillActivation);
        Assert.Single(pane.Executions);
        Assert.Equal([taskId], store.Queried);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task MissingStoreLeavesActivationsEmptyWithoutAffectingExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId));
        pane.Attach(taskId);
        Assert.Empty(pane.SkillActivations);
        Assert.False(pane.HasSkillActivations);
        Assert.Single(pane.Executions);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task StoreFailureDegradesToEmptyAndKeepsExecutions() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var store = new InspectorProbeSkillActivations
        {
            Reads = (_, _) => Task.FromException<IReadOnlyList<TaskSkillActivation>>(new InvalidOperationException("store outage"))
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId), skillActivations: store);
        pane.Attach(taskId);
        Assert.Empty(pane.SkillActivations);
        Assert.False(pane.HasSkillActivations);
        Assert.Single(pane.Executions);
        Assert.NotEqual(InspectorText.Get("AuditFailed"), pane.Status);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task SelectionShowsBoundedDetailExcerpt() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var activation = Activation(taskId, new string('x', 1200));
        var store = new InspectorProbeSkillActivations
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([activation])
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId), skillActivations: store);
        pane.Attach(taskId);
        var selected = Assert.Single(pane.SkillActivations);
        Assert.Equal(500, selected.Detail.Length);
        Assert.Same(selected, pane.SelectedSkillActivation);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task AttachClearsActivationsFromThePreviousTask() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var store = new InspectorProbeSkillActivations
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([Activation(taskId)])
        };
        using var pane = new AuditPaneViewModel(ReaderWithExecution(taskId), skillActivations: store);
        pane.Attach(taskId);
        Assert.Single(pane.SkillActivations);
        pane.Attach(Guid.NewGuid());
        Assert.Empty(pane.SkillActivations);
        Assert.False(pane.HasSkillActivations);
        Assert.Null(pane.SelectedSkillActivation);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CompositionRootInjectsTheRegisteredStore() => _session.Dispatch(() =>
    {
        var taskId = Guid.NewGuid();
        var store = new InspectorProbeSkillActivations
        {
            Reads = (_, _) => Task.FromResult<IReadOnlyList<TaskSkillActivation>>([Activation(taskId)])
        };
        var provider = new ServiceCollection()
            .AddSingleton<IToolExecutionAuditReader>(new InspectorProbeAuditReader())
            .AddSingleton<ITaskSkillActivationStore>(store)
            .AddSingleton<AuditPaneViewModel>()
            .BuildServiceProvider();
        using var pane = provider.GetRequiredService<AuditPaneViewModel>();
        pane.Attach(taskId);
        Assert.True(pane.HasSkillActivations);
        Assert.Single(pane.SkillActivations);
    }, TestContext.Current.CancellationToken);
}
