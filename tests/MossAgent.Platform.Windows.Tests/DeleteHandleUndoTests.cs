using MossAgent.Platform.Windows.Files;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DeleteHandleUndoTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UndoHashesAndDeletesSamePinnedFileAndRefusesExternalEdit(bool changed)
    {
        using var fixture = new AtomicWriteFixture();
        await fixture.SeedAsync();
        var original = await fixture.SnapshotAsync();
        var sessions = new CallbackPathDeleteSessions { Prepared = session =>
        {
            Assert.Equal(0, session.FileContent!.Position);
            Assert.ThrowsAny<IOException>(() => File.Delete(fixture.Path));
            Assert.ThrowsAny<IOException>(() => File.Move(fixture.Path, fixture.Path + ".external"));
            Assert.ThrowsAny<IOException>(() => File.WriteAllText(fixture.Path, "racing writer"));
        } };
        if (changed) await fixture.SeedAsync("external before open");
        var writer = new AtomicTextFileWriter(new(), deleteSessions: sessions);
        if (changed)
        {
            var exception = await Assert.ThrowsAsync<FileEditException>(() => writer.DeleteAsync(fixture.Path, original, fixture.Context, fixture.Token));
            Assert.Equal("file_changed", exception.ErrorCode);
            Assert.Equal("external before open", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        }
        else
        {
            Assert.False((await writer.DeleteAsync(fixture.Path, original, fixture.Context, fixture.Token)).Exists);
            Assert.False(File.Exists(fixture.Path));
        }
        Assert.Equal(1, sessions.Creates);
        Assert.False(sessions.Last!.FileContent!.CanRead);
    }
}
