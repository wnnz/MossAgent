using MossAgent.App;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProgramDataRootTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankEnvResolvesDefaultRoot(string? value)
    {
        Assert.Equal(new AppDataPaths().Root, Program.ResolveDataRoot(value));
    }

    [Fact]
    public void ExplicitRootIsPreserved()
    {
        var explicitRoot = Path.Combine(Path.GetTempPath(), "moss-explicit-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(Path.GetFullPath(explicitRoot), Program.ResolveDataRoot(explicitRoot));
    }
}
