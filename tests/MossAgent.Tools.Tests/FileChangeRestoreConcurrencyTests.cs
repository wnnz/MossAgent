using System.Text;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreConcurrencyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AConcurrentCommittedWrite_IsNeverOverwrittenOrDeleted(bool create)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: create);
        var original = await FileSnapshotReader.ReadAsync(fixture.Path, fixture.Token);
        var staged = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var approved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var competingWrite = fixture.Writer.WriteAsync(fixture.Path, original, fixture.Context, async (stream, token) =>
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes("concurrent sentinel"), token);
            staged.TrySetResult();
            await release.Task.WaitAsync(token);
        }, fixture.Token);
        try
        {
            await staged.Task.WaitAsync(fixture.Token);
            var restore = fixture.ExecuteAsync(change, remove: create, prompt: (_, _, _) =>
            {
                approved.TrySetResult();
                return ValueTask.FromResult(true);
            });
            await approved.Task.WaitAsync(fixture.Token);
            release.TrySetResult();
            await competingWrite;

            var result = await restore;

            Assert.Equal("file_changed", result.ErrorCode);
            Assert.Null(result.FileChanges);
            Assert.Equal("concurrent sentinel", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
            fixture.AssertNoTemporaryFiles();
        }
        finally { release.TrySetResult(); await competingWrite; }
    }
}
