using System.Text;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class AtomicWriteTargetSubstitutionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TargetChangedToOutsideJunctionIsRefusedWithoutTouchingOutside(bool existed)
    {
        using var fixture = new AtomicWriteFixture();
        using var outside = new AtomicWriteFixture();
        if (existed) await fixture.SeedAsync();
        await outside.SeedAsync("outside marker");
        var before = await outside.SnapshotAsync();
        var original = await fixture.SnapshotAsync();
        var link = Path.Combine(fixture.Root, "prepared-link");
        await TestDirectoryJunction.CreateAsync(link, outside.Parent, fixture.Token);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Writer.WriteAsync(
                fixture.Path, original, fixture.Context, async (stream, token) =>
                {
                    await stream.WriteAsync(Encoding.UTF8.GetBytes("updated"), token);
                    if (existed) File.Delete(fixture.Path);
                    Directory.Move(link, fixture.Path);
                }, fixture.Token));
            Assert.Equal(before, await outside.SnapshotAsync());
            Assert.Equal("outside marker", await File.ReadAllTextAsync(outside.Path, outside.Token));
            Assert.True(Directory.Exists(fixture.Path));
            outside.AssertNoTemporaryFiles();
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            if (Directory.Exists(fixture.Path)) Directory.Delete(fixture.Path);
        }
        fixture.AssertNoTemporaryFiles();
    }
}
