using Microsoft.Extensions.DependencyInjection;
using MossAgent.Infrastructure.Persistence;
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
}
