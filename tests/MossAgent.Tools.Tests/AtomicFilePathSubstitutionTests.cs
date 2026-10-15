using System.Text;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class AtomicFilePathSubstitutionTests
{
    [Fact]
    public async Task TargetReplacedWithEscapingJunction_IsRejectedBeforeCommit()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new FileEditingFixture();
        using var outside = new FileEditingFixture();
        await fixture.SeedAsync("original");
        await outside.SeedAsync("outside content");
        var original = await fixture.SnapshotAsync();
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Writer.WriteAsync(
                fixture.Path, original, fixture.Context, async (stream, token) =>
                {
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("agent content"), token);
                    File.Delete(fixture.Path);
                    await TestDirectoryJunction.CreateAsync(fixture.Path, outside.Root, token);
                }, fixture.Token));

            Assert.Equal("outside content", await outside.ContentsAsync());
            Assert.True(Directory.Exists(fixture.Path));
            fixture.AssertNoTemporaryFiles();
            outside.AssertNoTemporaryFiles();
        }
        finally
        {
            if (Directory.Exists(fixture.Path)) Directory.Delete(fixture.Path);
        }
    }
}
