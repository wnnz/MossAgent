using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class PluginSkillActivatorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.PluginSkills",
        Guid.NewGuid().ToString("N"));
    private readonly RecordingTaskSkillActivationStore _activations = new();
    private readonly ManualToolTimeProvider _time = new();

    private PluginSkillActivator Subject(IReadOnlyList<InstalledPlugin> rows,
        ITaskSkillActivationStore? activations = null, IPluginComponentSettings? settings = null) =>
        new(new StaticInstalledPluginStore(rows), activations ?? _activations, _root, _time, settings);

    private static InstalledPlugin Plugin(string name, InstalledPluginState state,
        params PluginComponentDescriptor[] components) =>
        new(Guid.NewGuid(), name, "1.0", new string('a', 64), $"{name}-1-0-aaaaaaaa", state,
            "/tmp/pkg.mossplugin", DateTimeOffset.UnixEpoch, components, [], PluginOverlaySource.None);

    private static PluginComponentDescriptor Skill(string relativePath) =>
        new(PluginComponentKind.Skill, relativePath, relativePath);

    private void WriteSkill(InstalledPlugin plugin, string relativePath, string content)
    {
        var path = Path.Combine(_root, plugin.InstallPath,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task EnabledSkillReturnsLabeledTextAndPersistsItsRow()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "do the bounded thing");
        var task = Guid.NewGuid();

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(task, TestContext.Current.CancellationToken);

        Assert.Equal("### demo v1.0 — skills/a/SKILL.md\ndo the bounded thing", text);
        var (taskId, rows) = Assert.Single(_activations.Saves);
        Assert.Equal(task, taskId);
        var row = Assert.Single(rows);
        Assert.Equal(task, row.TaskId);
        Assert.Equal(plugin.Id, row.PluginId);
        Assert.Equal("demo", row.PluginName);
        Assert.Equal("1.0", row.PluginVersion);
        Assert.Equal(new string('a', 64), row.ContentHash);
        Assert.Equal("skills/a/SKILL.md", row.ComponentPath);
        Assert.Equal("do the bounded thing", row.SkillText);
        Assert.Equal(_time.GetUtcNow(), row.ActivatedAt);
    }

    [Theory]
    [InlineData(InstalledPluginState.Installed)]
    [InlineData(InstalledPluginState.Disabled)]
    public async Task NonEnabledPluginsAreSkippedEvenWhenFilesExist(InstalledPluginState state)
    {
        var plugin = Plugin("demo", state, Skill("skills/a/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "must never enter a prompt");

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Empty(Assert.Single(_activations.Saves).Rows);
    }

    [Fact]
    public async Task NonSkillComponentsAreSkipped()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            new PluginComponentDescriptor(PluginComponentKind.McpServer, "server", "mcp.json"),
            new PluginComponentDescriptor(PluginComponentKind.Asset, "logo", "assets/logo.png"));

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Empty(Assert.Single(_activations.Saves).Rows);
    }

    [Fact]
    public async Task MissingSkillFileIsSkippedNotFailed()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Empty(Assert.Single(_activations.Saves).Rows);
    }

    [Theory]
    [InlineData(8192, true)]
    [InlineData(8193, false)]
    public async Task SkillFileSizeBoundIsEnforcedPerFile(int bytes, bool included)
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", new string('x', bytes));

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(included, text is not null);
        Assert.Equal(included ? 1 : 0, Assert.Single(_activations.Saves).Rows.Count);
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("skills/../../escape.md")]
    [InlineData("/escape.md")]
    public async Task RootedOrEscapingSkillPathsAreRejected(string relativePath)
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "escape.md"), "escaped sentinel content");
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill(relativePath));

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Empty(Assert.Single(_activations.Saves).Rows);
    }

    [Fact]
    public async Task TotalBudgetSkipsOverflowingSkillsDeterministicallyWithoutRows()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            Skill("skills/a/SKILL.md"), Skill("skills/b/SKILL.md"),
            Skill("skills/c/SKILL.md"), Skill("skills/d/SKILL.md"));
        foreach (var letter in new[] { "a", "b", "c", "d" })
            WriteSkill(plugin, $"skills/{letter}/SKILL.md", new string(letter[0], 8000));

        var text = await Subject([plugin]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Contains("skills/a/SKILL.md", text, StringComparison.Ordinal);
        Assert.Contains("skills/b/SKILL.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("skills/c/SKILL.md", text, StringComparison.Ordinal);
        Assert.DoesNotContain("skills/d/SKILL.md", text, StringComparison.Ordinal);
        var rows = Assert.Single(_activations.Saves).Rows;
        Assert.Equal(["skills/a/SKILL.md", "skills/b/SKILL.md"], rows.Select(row => row.ComponentPath).ToArray());
    }

    [Fact]
    public async Task EntriesSortByPluginNameThenComponentPath()
    {
        var zeta = Plugin("zeta", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));
        var alpha = Plugin("alpha", InstalledPluginState.Enabled,
            Skill("skills/b/SKILL.md"), Skill("skills/a/SKILL.md"));
        WriteSkill(zeta, "skills/a/SKILL.md", "zeta body");
        WriteSkill(alpha, "skills/a/SKILL.md", "alpha a");
        WriteSkill(alpha, "skills/b/SKILL.md", "alpha b");

        var text = await Subject([zeta, alpha]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        var alphaA = text!.IndexOf("### alpha v1.0 — skills/a/SKILL.md", StringComparison.Ordinal);
        var alphaB = text.IndexOf("### alpha v1.0 — skills/b/SKILL.md", StringComparison.Ordinal);
        var zetaA = text.IndexOf("### zeta v1.0 — skills/a/SKILL.md", StringComparison.Ordinal);
        Assert.True(alphaA > -1 && alphaA < alphaB && alphaB < zetaA);
    }

    [Fact]
    public async Task ActivationStoreFailurePropagates()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "body");
        var failure = new InvalidOperationException("snapshot write failed");

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Subject([plugin], new FailingTaskSkillActivationStore(failure))
                .BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
    }

    [Fact]
    public async Task NoEnabledPluginsReturnsNullAndClearsTheTaskSnapshot()
    {
        var text = await Subject([]).BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Null(text);
        Assert.Empty(Assert.Single(_activations.Saves).Rows);
    }

    [Fact]
    public async Task DisabledSkillComponentsAreSkippedAndLeaveNoRows()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            Skill("skills/a/SKILL.md"), Skill("skills/b/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "alpha body");
        WriteSkill(plugin, "skills/b/SKILL.md", "beta body");
        var settings = new RecordingPluginComponentSettings();
        settings.Seed(plugin.Id, "skills/b/SKILL.md", false);

        var text = await Subject([plugin], settings: settings)
            .BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal("### demo v1.0 — skills/a/SKILL.md\nalpha body", text);
        Assert.Equal(["skills/a/SKILL.md"],
            Assert.Single(_activations.Saves).Rows.Select(row => row.ComponentPath).ToArray());
        Assert.Equal(1, settings.ListCalls);
    }

    [Fact]
    public async Task ExplicitlyReEnabledAndAbsentChoicesAreBothIncluded()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled,
            Skill("skills/a/SKILL.md"), Skill("skills/b/SKILL.md"), Skill("skills/c/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "alpha body");
        WriteSkill(plugin, "skills/b/SKILL.md", "beta body");
        WriteSkill(plugin, "skills/c/SKILL.md", "gamma body");
        var settings = new RecordingPluginComponentSettings();
        settings.Seed(plugin.Id, "skills/b/SKILL.md", true);
        settings.Seed(Guid.NewGuid(), "skills/b/SKILL.md", false);

        var text = await Subject([plugin], settings: settings)
            .BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Contains("skills/a/SKILL.md", text, StringComparison.Ordinal);
        Assert.Contains("skills/b/SKILL.md", text, StringComparison.Ordinal);
        Assert.Contains("skills/c/SKILL.md", text, StringComparison.Ordinal);
        Assert.Equal(3, Assert.Single(_activations.Saves).Rows.Count);
    }

    [Fact]
    public async Task ComponentSettingsFailurePropagatesAndNoSnapshotIsWritten()
    {
        var plugin = Plugin("demo", InstalledPluginState.Enabled, Skill("skills/a/SKILL.md"));
        WriteSkill(plugin, "skills/a/SKILL.md", "body");
        var failure = new InvalidOperationException("overrides read failed");
        var settings = new RecordingPluginComponentSettings { ListFailure = failure };

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Subject([plugin], settings: settings)
                .BuildSkillInstructionsAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));

        Assert.Same(failure, thrown);
        Assert.Empty(_activations.Saves);
    }
}
