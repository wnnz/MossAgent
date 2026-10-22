using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

/// <summary>
/// The editor binds the plan-level authorization and model snapshots on save, and the policy choice
/// is explicit rather than derived from the current project state.
/// </summary>
public sealed class AutomationScopeEditorTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly Guid ProviderId = Guid.NewGuid();
    private static readonly Guid ModelId = Guid.NewGuid();

    [Fact]
    public void NewPlan_BindsFixedSnapshotWithProjectRevisionAndModelParameters()
    {
        var editor = Open(null);

        var built = editor.Build(Now);

        var scope = Assert.IsType<AutomationScopeSnapshot>(built.Scope);
        Assert.Equal(AutomationScopePolicy.FixedSnapshot, scope.Policy);
        Assert.Equal(4, scope.ProjectRevision);
        Assert.Equal([@"C:\workspace"], scope.AuthorizedRoots);
        var model = Assert.IsType<AutomationModelSnapshot>(built.Model);
        Assert.Equal("high", model.ReasoningEffort);
        Assert.Equal(0.3, model.Temperature);
        Assert.Equal(0.8, model.TopP);
        Assert.Equal("{\"verbosity\":\"low\"}", model.AdvancedParametersJson);
    }

    [Fact]
    public void FollowProject_IsOnlyAppliedWhenTheUserSelectsIt()
    {
        var editor = Open(null);
        editor.SelectedScopePolicy = Assert.Single(editor.ScopePolicies,
            item => item.Value == AutomationScopePolicy.FollowProject);

        Assert.Equal(AutomationScopePolicy.FollowProject, editor.Build(Now).Scope!.Policy);
    }

    [Fact]
    public void ExistingPlan_KeepsItsSavedScopePolicyAndLegacyPlanDefaultsToFixed()
    {
        var legacy = Schedule() with { Scope = null, Model = null };
        var following = Schedule() with
        { Scope = new AutomationScopeSnapshot(4, [@"C:\workspace"], AutomationScopePolicy.FollowProject) };

        Assert.Equal(AutomationScopePolicy.FixedSnapshot, Open(legacy).SelectedScopePolicy!.Value);
        Assert.Equal(AutomationScopePolicy.FollowProject, Open(following).SelectedScopePolicy!.Value);
    }

    [Fact]
    public void MissingScopeSelection_BlocksBuildInsteadOfSavingAnUnboundPlan()
    {
        var editor = Open(null);
        editor.SelectedScopePolicy = null;

        Assert.Throws<ArgumentException>(() => editor.Build(Now));
    }

    private static AutomationEditorViewModel Open(AutomationSchedule? schedule)
    {
        var editor = new AutomationEditorViewModel(new PreviewTestTimeProvider(Now));
        var project = new ProjectProfile(ProjectId, "scope", @"C:\workspace", [@"C:\workspace"], Now)
        { AuthorizationRevision = 4 };
        var provider = new AiProvider(ProviderId, "fixture", ProviderProtocol.OpenAiResponses,
            new Uri("https://example.test/v1/"), "fixture-key", null, true, true,
            new Dictionary<string, string>());
        editor.Open(schedule, [project], [new ModelPickerItemViewModel(provider, Model())]);
        if (schedule is null)
        {
            editor.Name = "计划";
            editor.Prompt = "只读检查。";
            editor.SelectedProject = Assert.Single(editor.Projects);
            editor.SelectedModel = Assert.Single(editor.Models);
        }
        return editor;
    }

    private static AutomationSchedule Schedule() => new(Guid.NewGuid(), "计划", "只读检查。", ProjectId,
        ProviderId, ModelId, AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday,
        TimeZoneInfo.Utc.Id, ApprovalPolicy.ReadOnly, 30, false, Now.AddDays(1),
        Scope: new AutomationScopeSnapshot(1, [@"C:\workspace"], AutomationScopePolicy.FixedSnapshot),
        Model: new AutomationModelSnapshot("medium", null, null, "{}"));

    private static ModelProfile Model() => new(ModelId, ProviderId, "fixture", "fixture", 8192, 1024,
        "high", 0.3, 0.8, false, false, true, true, "{\"verbosity\":\"low\"}");
}
