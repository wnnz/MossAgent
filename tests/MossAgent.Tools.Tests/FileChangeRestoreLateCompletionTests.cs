using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreLateCompletionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ACommittedRestoreOrRemoval_RetainsItsInverseEvenAfterLateInterruption(bool create, bool cancelCaller)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: create);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var clock = new FileChangeRestoreLateTimeProvider(cancelCaller ? cancellation.Cancel : null);

        var result = await fixture.ExecuteAsync(change, remove: create,
            context: fixture.Context with { ApprovalPolicy = MossAgent.Domain.ApprovalPolicy.AskEveryTime, Timeout = TimeSpan.FromDays(1) },
            token: cancellation.Token, timeProvider: clock);

        Assert.False(result.IsSuccess);
        Assert.Equal(cancelCaller ? "tool_cancelled" : "tool_timeout", result.ErrorCode);
        var inverse = Assert.Single(result.FileChanges!);
        Assert.True(inverse.IsValid);
        Assert.Equal(change.AfterSha256, inverse.BeforeSha256);
        Assert.Equal(change.BeforeSha256, inverse.AfterSha256);
        Assert.Equal(create, inverse.AfterDeleted);
        if (create) Assert.False(File.Exists(fixture.Path));
        else Assert.Equal("original\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
