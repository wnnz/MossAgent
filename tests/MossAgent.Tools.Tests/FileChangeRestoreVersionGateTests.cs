using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreVersionGateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LowercaseReviewedHashes_RetainCaseInsensitiveVersionSemantics(bool create)
    {
        using var fixture = new FileChangeRestoreFixture();
        var recorded = await fixture.ChangeAsync(create: create);
        var change = recorded with
        {
            BeforeSha256 = recorded.BeforeSha256?.ToLowerInvariant(),
            AfterSha256 = recorded.AfterSha256!.ToLowerInvariant(),
            BeforeSnapshot = recorded.BeforeSnapshot is { } before ? before with { Sha256 = before.Sha256.ToLowerInvariant() } : null,
            AfterSnapshot = recorded.AfterSnapshot! with { Sha256 = recorded.AfterSnapshot.Sha256.ToLowerInvariant() }
        };

        var result = await fixture.ExecuteAsync(change, remove: create);

        Assert.True(change.IsValid);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(Assert.Single(result.FileChanges!).IsValid);
        if (create) Assert.False(File.Exists(fixture.Path));
        else Assert.Equal("original\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
