using MossAgent.Domain.Plugins;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

/// <summary>Field-wise row comparison: the record's list members compare by reference, so
/// persisted rows need member equality plus sequence equality on components and servers.</summary>
internal static class PluginRowAssert
{
    public static InstalledPlugin Equal(InstalledPlugin expected, InstalledPlugin? actual)
    {
        var row = Assert.IsType<InstalledPlugin>(actual);
        Assert.Equal(expected.Id, row.Id);
        Assert.Equal(expected.Name, row.Name);
        Assert.Equal(expected.Version, row.Version);
        Assert.Equal(expected.ContentHash, row.ContentHash);
        Assert.Equal(expected.InstallPath, row.InstallPath);
        Assert.Equal(expected.State, row.State);
        Assert.Equal(expected.SourcePath, row.SourcePath);
        Assert.Equal(expected.InstalledAt, row.InstalledAt);
        Assert.Equal(expected.Components, row.Components);
        Assert.Equal(expected.DeclaredMcpServers, row.DeclaredMcpServers);
        Assert.Equal(expected.OverlaySource, row.OverlaySource);
        return row;
    }
}
