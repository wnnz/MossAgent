using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Plugins;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Infrastructure.Plugins;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AppDataDirectoryTests
{
    [Fact]
    public void CreateServices_UsesInjectedDataRoot()
    {
        var expectedRoot = Path.Combine(Path.GetTempPath(), $"MossAgent-{Guid.NewGuid():N}");

        using var services = AppComposition.CreateServices(expectedRoot);
        var paths = services.GetRequiredService<AppDataPaths>();

        Assert.Equal(expectedRoot, paths.Root);
    }

    [Fact]
    public async Task CreateServices_ResolvesThePluginInstallChain()
    {
        var expectedRoot = Path.Combine(Path.GetTempPath(), $"MossAgent-{Guid.NewGuid():N}");

        await using var services = AppComposition.CreateServices(expectedRoot);
        var installer = services.GetRequiredService<IPluginPackageInstaller>();

        Assert.IsType<PluginPackageInstaller>(installer);
        Assert.IsType<ZipPluginPackagePreviewer>(services.GetRequiredService<IPluginPackagePreviewer>());
        Assert.IsType<SqliteInstalledPluginStore>(services.GetRequiredService<IInstalledPluginStore>());
        Assert.IsType<SqlitePluginComponentSettings>(services.GetRequiredService<IPluginComponentSettings>());
        Assert.NotNull(services.GetRequiredService<IPluginSkillActivator>());
        Assert.NotNull(services.GetRequiredService<PluginPackagesViewModel>());
        Assert.Equal(Path.Combine(expectedRoot, "plugins"), services.GetRequiredService<AppDataPaths>().PluginsPath);
        Assert.NotNull(services.GetRequiredService<MossAgent.App.Services.AppStartupService>());
    }
}
