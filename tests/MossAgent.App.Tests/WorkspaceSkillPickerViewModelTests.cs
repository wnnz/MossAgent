using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Domain.Plugins;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceSkillPickerViewModelTests
{
    private static InstalledPlugin Plugin(string name, InstalledPluginState state,
        params PluginComponentDescriptor[] components) =>
        new(Guid.NewGuid(), name, "1.0.0", "hashabc", $"plugins/{name}-1.0.0-hashabc8", state,
            $@"D:\packages\{name}.mossplugin", DateTimeOffset.UtcNow, components,
            [], PluginOverlaySource.None);

    private static PluginComponentDescriptor Skill(string name, string path) =>
        new(PluginComponentKind.Skill, name, path);

    private static AgentTask NewTask(string title = "conversation")
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentTask(Guid.NewGuid(), null, title, ApprovalPolicy.ReadOnly,
            AgentTaskStatus.Running, now, now, null);
    }

    private static (WorkspaceSkillPickerViewModel Picker, RecordingPluginPackageInstaller Installer,
        RecordingPluginComponentSettings Settings, RecordingTaskSkillOverrides Overrides,
        WorkspaceCatalogViewModel Catalog) Create(params InstalledPlugin[] installed) =>
        CreateCore(null, installed);

    private static (WorkspaceSkillPickerViewModel, RecordingPluginPackageInstaller,
        RecordingPluginComponentSettings, RecordingTaskSkillOverrides,
        WorkspaceCatalogViewModel) CreateCore(AgentTask? selected, InstalledPlugin[] installed)
    {
        var repository = new TestWorkspaceRepository();
        var catalog = new WorkspaceCatalogViewModel(repository, null,
            new WorkspaceProjectService(repository, new TestFolderPickerService()));
        if (selected is not null) catalog.UpsertTask(selected);
        var installer = new RecordingPluginPackageInstaller { Installed = installed };
        var settings = new RecordingPluginComponentSettings();
        var overrides = new RecordingTaskSkillOverrides();
        var picker = new WorkspaceSkillPickerViewModel(installer, settings, overrides, catalog);
        return (picker, installer, settings, overrides, catalog);
    }

    [Fact]
    public async Task EffectiveStateResolvesTaskOverGlobalOverDefaultPerComponent()
    {
        var task = NewTask();
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            Skill("a", "skills/a/SKILL.md"), Skill("b", "skills/b/SKILL.md"),
            Skill("c", "skills/c/SKILL.md"), Skill("d", "skills/d/SKILL.md"));
        var plain = Plugin("plain", InstalledPluginState.Enabled,
            new PluginComponentDescriptor(PluginComponentKind.Asset, "logo", "assets/logo.png"));
        var ghost = Plugin("ghost", InstalledPluginState.Disabled, Skill("x", "skills/x/SKILL.md"));
        var (picker, _, settings, overrides, _) = CreateCore(task, [plugin, plain, ghost]);
        settings.Seed(plugin.Id, "skills/b/SKILL.md", false);
        settings.Seed(plugin.Id, "skills/d/SKILL.md", false);
        overrides.Seed(task.Id, plugin.Id, "skills/a/SKILL.md", false);
        overrides.Seed(task.Id, plugin.Id, "skills/b/SKILL.md", true);
        using var _ = picker;

        await picker.ReloadAsync();

        var group = Assert.Single(picker.Groups);
        Assert.Equal("demo v1.0.0", group.Header);
        var rows = group.Items;
        Assert.Equal(4, rows.Count);
        Assert.False(rows[0].IsEnabled);   // task row disables it
        Assert.True(rows[1].IsEnabled);    // task row re-enables a globally disabled skill
        Assert.True(rows[2].IsEnabled);    // no rows: default-enabled
        Assert.False(rows[3].IsEnabled);   // global row disables it
        Assert.Equal(2, picker.EnabledSkills);
        Assert.Equal(4, picker.TotalSkills);
        Assert.True(picker.HasSkills);
        Assert.Equal("2/4", picker.Summary);
        Assert.False(picker.ShowEmptyHint);
    }

    [Fact]
    public async Task ToggleOnBoundTaskWritesTheTaskRowThroughThePort()
    {
        var task = NewTask();
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, _) = CreateCore(task, [plugin]);
        using var _ = picker;
        await picker.ReloadAsync();
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        row.IsEnabled = false;

        await picker.ToggleSkillCommand.ExecuteAsync(row);

        Assert.Equal([(task.Id, plugin.Id, "skills/a/SKILL.md", false)], overrides.Writes);
        Assert.False(row.IsEnabled);
        Assert.False(picker.StatusIsError);
        Assert.Null(picker.Status);
        Assert.Equal(0, picker.PendingDraftCount);
    }

    [Fact]
    public async Task ToggleFailureRestoresTheRowAndSurfacesALocalizedError()
    {
        var task = NewTask();
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, _) = CreateCore(task, [plugin]);
        overrides.WriteFailure = new InvalidOperationException("synthetic-secret");
        using var _ = picker;
        await picker.ReloadAsync();
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        row.IsEnabled = false;

        await picker.ToggleSkillCommand.ExecuteAsync(row);

        Assert.Empty(overrides.Writes);
        Assert.True(row.IsEnabled);
        Assert.True(picker.StatusIsError);
        Assert.Equal("技能选择保存失败，请重试。", picker.Status);
        Assert.DoesNotContain("synthetic-secret", picker.Status);
    }

    [Fact]
    public async Task UnboundConversationKeepsADraftThatFlushesOnTaskAssignment()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, catalog) = Create(plugin);
        using var _ = picker;
        await picker.ReloadAsync();
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        row.IsEnabled = false;

        await picker.ToggleSkillCommand.ExecuteAsync(row);

        Assert.Empty(overrides.Writes);
        Assert.Equal(1, picker.PendingDraftCount);
        var task = NewTask();
        picker.RememberTask(task.Id);
        await picker.FlushAsync();
        catalog.UpsertTask(task);
        await picker.ReloadAsync();

        Assert.Equal([(task.Id, plugin.Id, "skills/a/SKILL.md", false)], overrides.Writes);
        Assert.Equal(0, picker.PendingDraftCount);
        row = Assert.Single(Assert.Single(picker.Groups).Items);
        Assert.False(row.IsEnabled);
    }

    [Fact]
    public async Task ResetOnBoundTaskClearsItsRowsAndRestoresTheGlobalLayer()
    {
        var task = NewTask();
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, _) = CreateCore(task, [plugin]);
        overrides.Seed(task.Id, plugin.Id, "skills/a/SKILL.md", false);
        using var _ = picker;
        await picker.ReloadAsync();
        Assert.False(Assert.Single(Assert.Single(picker.Groups).Items).IsEnabled);

        await picker.ResetCommand.ExecuteAsync(null);

        Assert.Equal([task.Id], overrides.ClearedTasks);
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        Assert.True(row.IsEnabled);
        Assert.Null(picker.Status);
        Assert.Empty(await overrides.ListForTaskAsync(task.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResetWithoutATaskClearsOnlyTheDraft()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, _) = Create(plugin);
        using var _ = picker;
        await picker.ReloadAsync();
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        row.IsEnabled = false;
        await picker.ToggleSkillCommand.ExecuteAsync(row);
        Assert.Equal(1, picker.PendingDraftCount);

        await picker.ResetCommand.ExecuteAsync(null);

        Assert.Equal(0, picker.PendingDraftCount);
        Assert.Empty(overrides.ClearedTasks);
        Assert.Empty(overrides.Writes);
        Assert.True(Assert.Single(Assert.Single(picker.Groups).Items).IsEnabled);
    }

    [Fact]
    public async Task ResetFailureSurfacesAnErrorAndKeepsTheStoredRows()
    {
        var task = NewTask();
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, _, _, overrides, _) = CreateCore(task, [plugin]);
        overrides.Seed(task.Id, plugin.Id, "skills/a/SKILL.md", false);
        using var _ = picker;
        await picker.ReloadAsync();
        overrides.ClearFailure = new InvalidOperationException("synthetic-secret");

        await picker.ResetCommand.ExecuteAsync(null);

        Assert.True(picker.StatusIsError);
        Assert.Equal("技能选择保存失败，请重试。", picker.Status);
        Assert.DoesNotContain("synthetic-secret", picker.Status);
        Assert.False(Assert.Single(Assert.Single(picker.Groups).Items).IsEnabled);
        Assert.Empty(overrides.ClearedTasks);
    }

    [Fact]
    public async Task NoEnabledSkillComponentsShowsTheEmptyHint()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            new PluginComponentDescriptor(PluginComponentKind.Asset, "logo", "assets/logo.png"));
        var ghost = Plugin("ghost", InstalledPluginState.Drifted, Skill("x", "skills/x/SKILL.md"));
        var (picker, _, _, _, _) = Create(plugin, ghost);
        using var _ = picker;

        await picker.ReloadAsync();

        Assert.False(picker.HasSkills);
        Assert.True(picker.ShowEmptyHint);
        Assert.Equal("没有已启用的插件技能", picker.EmptyHint);
        Assert.Equal(string.Empty, picker.Summary);
        Assert.False(picker.ResetCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadFailureSurfacesALocalizedErrorWithoutExceptionDetails()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, installer, _, _, _) = Create(plugin);
        installer.ListFailure = new InvalidOperationException("synthetic-secret");
        using var _ = picker;

        await picker.ReloadAsync();

        Assert.True(picker.HasLoadError);
        Assert.True(picker.StatusIsError);
        Assert.Equal("插件技能加载失败，请刷新重试。", picker.Status);
        Assert.DoesNotContain("synthetic-secret", picker.Status);
        Assert.False(picker.ShowEmptyHint);
    }

    [Fact]
    public async Task DisposeDisarmsCommandsAndIgnoresLateSelectionChanges()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("a", "skills/a/SKILL.md"));
        var (picker, installer, _, overrides, catalog) = Create(plugin);
        await picker.ReloadAsync();
        var row = Assert.Single(Assert.Single(picker.Groups).Items);
        var listCalls = installer.ListCalls;
        var overrideReads = overrides.ListCalls;

        picker.Dispose();

        Assert.False(picker.ToggleSkillCommand.CanExecute(row));
        Assert.False(picker.ResetCommand.CanExecute(null));
        catalog.UpsertTask(NewTask());
        await Task.Delay(30, TestContext.Current.CancellationToken);
        Assert.Equal(listCalls, installer.ListCalls);
        Assert.Equal(overrideReads, overrides.ListCalls);
    }
}
