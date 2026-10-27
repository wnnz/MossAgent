using MossAgent.App.ViewModels;
using MossAgent.Application.Plugins;
using MossAgent.Domain.Plugins;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class PluginPackagesViewModelTests
{
    private const string PackagePath = @"D:\packages\demo-plugin-1.2.3.mossplugin";

    private static InstalledPlugin Plugin(string name = "demo", InstalledPluginState state = InstalledPluginState.Installed) =>
        new(Guid.NewGuid(), name, "1.0.0", "hashabc", $"plugins/{name}-1.0.0-hashabc8", state,
            $@"D:\packages\{name}.mossplugin", DateTimeOffset.UtcNow,
            [new PluginComponentDescriptor(PluginComponentKind.Skill, "review", "skills/review/SKILL.md")],
            [], PluginOverlaySource.None);

    private static PluginPackagePreview Preview => new("demo-plugin", "1.2.3", "demo package", PackagePath,
        "deadbeefcafe1234567890", 2, 512,
        [new PluginComponentDescriptor(PluginComponentKind.Skill, "review", "skills/review/SKILL.md"),
            new PluginComponentDescriptor(PluginComponentKind.McpServer, "docs", "mcp.json")],
        PluginOverlaySource.None, [new PluginMcpServerDeclaration("docs", "stdio")]);

    private static (PluginPackagesViewModel ViewModel, RecordingPluginPackageInstaller Installer,
        RecordingPluginPackagePreviewer Previewer, PluginPackagePickerProbe Picker) Create(
            params InstalledPlugin[] installed)
    {
        var installer = new RecordingPluginPackageInstaller { Installed = installed };
        var previewer = new RecordingPluginPackagePreviewer();
        var picker = new PluginPackagePickerProbe { Paths = [PackagePath] };
        return (new PluginPackagesViewModel(installer, previewer, picker), installer, previewer, picker);
    }

    [Fact]
    public async Task PickPreviewAndConfirmInstallAddsRowAndReportsSuccess()
    {
        var plugin = Plugin("demo-plugin");
        var (viewModel, installer, previewer, picker) = Create();
        previewer.Result = Preview;
        installer.InstallHandler = _ =>
        {
            installer.Installed = [plugin];
            return plugin;
        };
        using var _ = viewModel;

        await viewModel.PickPackageCommand.ExecuteAsync(null);

        Assert.Equal(1, picker.PackageCalls);
        Assert.Equal([PackagePath], previewer.Paths);
        Assert.NotNull(viewModel.PendingPreview);
        var pending = viewModel.PendingPreview;
        Assert.True(viewModel.HasPendingPreview);
        Assert.Equal("demo-plugin 1.2.3", pending.Title);
        Assert.Equal(PackagePath, pending.PackagePath);
        Assert.Equal("请核对组件与来源后确认安装 demo-plugin 1.2.3。", viewModel.Status);
        Assert.False(viewModel.HasStatusError);
        Assert.True(viewModel.ConfirmInstallCommand.CanExecute(null));

        await viewModel.ConfirmInstallCommand.ExecuteAsync(null);

        Assert.Equal([PackagePath], installer.InstallPaths);
        Assert.Null(viewModel.PendingPreview);
        var row = Assert.Single(viewModel.Items);
        Assert.Equal("demo-plugin", row.Name);
        Assert.Equal("已安装", row.StateText);
        Assert.Equal("已安装插件 demo-plugin 1.0.0。", viewModel.Status);
        Assert.False(viewModel.HasStatusError);
    }

    [Theory]
    [InlineData(PluginPackageErrorCode.Unreadable, "无法读取插件包，请确认文件未损坏后重试。")]
    [InlineData(PluginPackageErrorCode.EntryInvalid, "插件包包含不安全或无效的归档条目，已拒绝。")]
    [InlineData(PluginPackageErrorCode.TooLarge, "插件包超出大小限制，已拒绝。")]
    [InlineData(PluginPackageErrorCode.ManifestMissing, "插件包缺少 plugin.json 清单。")]
    [InlineData(PluginPackageErrorCode.ManifestInvalid, "插件清单 plugin.json 无效，已拒绝。")]
    [InlineData(PluginPackageErrorCode.McpInvalid, "插件声明的 mcp.json 无效，已拒绝。")]
    public async Task PreviewRejectionMapsCodeToLocalizedErrorStatus(string code, string expected)
    {
        var (viewModel, _, previewer, _) = Create();
        previewer.Failure = new PluginPackageException(code, "synthetic-secret");
        using var _ = viewModel;

        await viewModel.PickPackageCommand.ExecuteAsync(null);

        Assert.Null(viewModel.PendingPreview);
        Assert.True(viewModel.HasStatusError);
        Assert.Equal(expected, viewModel.Status);
        Assert.DoesNotContain("synthetic-secret", viewModel.Status);
    }

    [Fact]
    public async Task ConfirmInstallConflictShowsConflictStatusAndKeepsListUnchanged()
    {
        var existing = Plugin();
        var (viewModel, installer, previewer, _) = Create(existing);
        previewer.Result = Preview;
        installer.InstallFailure = new PluginPackageException(PluginPackageErrorCode.InstallConflict, "synthetic");
        using var _ = viewModel;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        await viewModel.PickPackageCommand.ExecuteAsync(null);

        await viewModel.ConfirmInstallCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Items);
        Assert.True(viewModel.HasStatusError);
        Assert.Equal("已存在同名同版本但内容不同的插件，请先卸载现有版本。", viewModel.Status);
    }

    [Fact]
    public async Task CancelInstallClearsPendingPreviewAndKeepsStatusHint()
    {
        var (viewModel, installer, previewer, _) = Create();
        previewer.Result = Preview;
        using var _ = viewModel;
        await viewModel.PickPackageCommand.ExecuteAsync(null);
        Assert.True(viewModel.CancelInstallCommand.CanExecute(null));

        viewModel.CancelInstallCommand.Execute(null);

        Assert.Null(viewModel.PendingPreview);
        Assert.False(viewModel.HasPendingPreview);
        Assert.Equal("已取消安装。", viewModel.Status);
        Assert.False(viewModel.HasStatusError);
        Assert.Empty(installer.InstallPaths);
    }

    [Fact]
    public async Task ToggleEnabledRoundtripsThroughPortAndUpdatesRow()
    {
        var plugin = Plugin();
        var (viewModel, installer, _, _) = Create(plugin);
        installer.ToggleHandler = (id, enable) => plugin with
        {
            Id = id,
            State = enable ? InstalledPluginState.Enabled : InstalledPluginState.Disabled
        };
        using var _ = viewModel;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(viewModel.Items);

        await viewModel.ToggleEnabledCommand.ExecuteAsync(row);

        Assert.Equal([plugin.Id], installer.EnabledIds);
        Assert.True(row.IsEnabled);
        Assert.Equal("已启用", row.StateText);
        Assert.Equal("已启用插件 demo。其技能指令将注入后续任务；其余组件暂不激活。", viewModel.Status);

        await viewModel.ToggleEnabledCommand.ExecuteAsync(row);

        Assert.Equal([plugin.Id], installer.DisabledIds);
        Assert.False(row.IsEnabled);
        Assert.Equal("已停用", row.StateText);
        Assert.Equal("已停用插件 demo。", viewModel.Status);
    }

    [Fact]
    public async Task UninstallRequiresSecondClickThenRemovesRowAndReportsKeptPayload()
    {
        var plugin = Plugin();
        var (viewModel, installer, _, _) = Create(plugin);
        installer.UninstallHandler = _ => new PluginUninstallOutcome(plugin, PayloadRemoved: false);
        using var _ = viewModel;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(viewModel.Items);
        Assert.Equal("卸载", row.UninstallLabel);

        await viewModel.UninstallCommand.ExecuteAsync(row);

        Assert.Empty(installer.UninstalledIds);
        Assert.True(row.IsUninstallPending);
        Assert.Equal("确认卸载", row.UninstallLabel);
        Assert.Equal("再次点击“确认卸载”以卸载 demo。", viewModel.Status);
        Assert.Single(viewModel.Items);

        await viewModel.UninstallCommand.ExecuteAsync(row);

        Assert.Equal([plugin.Id], installer.UninstalledIds);
        Assert.Empty(viewModel.Items);
        Assert.Equal("已卸载插件 demo，但载荷目录删除失败，已保留为残留，启动核对时将报告。", viewModel.Status);
        Assert.False(viewModel.HasStatusError);
    }

    [Fact]
    public async Task ToggleOrUninstallOnVanishedRowReportsTargetUnavailable()
    {
        var plugin = Plugin();
        var (viewModel, installer, _, _) = Create(plugin);
        using var _ = viewModel;
        await viewModel.RefreshCommand.ExecuteAsync(null);
        var row = Assert.Single(viewModel.Items);
        installer.Installed = [];

        await viewModel.ToggleEnabledCommand.ExecuteAsync(row);

        Assert.Equal("该插件已不存在，请刷新列表后重试。", viewModel.Status);
        Assert.True(viewModel.HasStatusError);
        Assert.Empty(viewModel.Items);

        installer.Installed = [plugin];
        await viewModel.RefreshCommand.ExecuteAsync(null);
        row = Assert.Single(viewModel.Items);
        installer.Installed = [];

        await viewModel.UninstallCommand.ExecuteAsync(row);
        Assert.Empty(installer.UninstalledIds);

        await viewModel.UninstallCommand.ExecuteAsync(row);

        Assert.Equal([plugin.Id], installer.UninstalledIds);
        Assert.Equal("该插件已不存在，请刷新列表后重试。", viewModel.Status);
        Assert.True(viewModel.HasStatusError);
        Assert.Empty(viewModel.Items);
    }

    [Fact]
    public async Task RefreshLoadsInstalledRowsWithLocalizedPresentation()
    {
        var (viewModel, installer, _, _) = Create(
            Plugin("alpha", InstalledPluginState.Enabled),
            Plugin("beta", InstalledPluginState.Disabled));
        using var _ = viewModel;

        await viewModel.RefreshCommand.ExecuteAsync(null);

        Assert.True(installer.ListCalls >= 1);
        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal("已启用", viewModel.Items[0].StateText);
        Assert.Equal("已停用", viewModel.Items[1].StateText);
        Assert.Equal("版本 1.0.0 · 1 个组件 · plugins/alpha-1.0.0-hashabc8", viewModel.Items[0].Subtitle);
        Assert.Equal("插件列表已刷新。", viewModel.Status);
        Assert.False(viewModel.HasStatusError);
    }

    [Fact]
    public async Task EmptyPickerSelectionDoesNotStartPreview()
    {
        var (viewModel, _, previewer, picker) = Create();
        picker.Paths = [];
        using var _ = viewModel;

        await viewModel.PickPackageCommand.ExecuteAsync(null);

        Assert.Equal(1, picker.PackageCalls);
        Assert.Empty(previewer.Paths);
        Assert.Null(viewModel.PendingPreview);
    }

    [Fact]
    public async Task DisposeDisablesCommandsAndIgnoresLateInteractions()
    {
        var (viewModel, installer, previewer, _) = Create(Plugin());
        previewer.Result = Preview;
        await viewModel.RefreshCommand.ExecuteAsync(null);

        viewModel.Dispose();

        Assert.False(viewModel.PickPackageCommand.CanExecute(null));
        Assert.False(viewModel.ToggleEnabledCommand.CanExecute(viewModel.Items[0]));
        await viewModel.PickPackageCommand.ExecuteAsync(null);
        Assert.Empty(previewer.Paths);
        Assert.Empty(installer.InstallPaths);
    }
}
