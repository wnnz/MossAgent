using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeRestoreSafetyTests
{
    [Theory]
    [InlineData("readonly", "capability_denied")]
    [InlineData("denied", "approval_denied")]
    [InlineData("untrusted", "path_denied")]
    [InlineData("outside", "path_denied")]
    [InlineData("traversal", "invalid_arguments")]
    [InlineData("legacy", "snapshot_unavailable")]
    [InlineData("timeout", "tool_timeout")]
    [InlineData("mismatched-version", "invalid_arguments")]
    public async Task Restore_RejectionNeverChangesTheCurrentFile(string scenario, string error)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync();
        var context = fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        if (scenario == "readonly") context = context with { ApprovalPolicy = ApprovalPolicy.ReadOnly };
        else if (scenario == "outside") change = change with { Path = System.IO.Path.Combine(fixture.Root, "..", "outside.txt") };
        else if (scenario == "traversal") change = change with { BeforeSnapshot = change.BeforeSnapshot! with { FileName = "../escape.snapshot" } };
        else if (scenario == "legacy") change = change with { BeforeSnapshot = null, AfterSnapshot = null };
        else if (scenario == "timeout") context = context with { Timeout = TimeSpan.Zero };
        else if (scenario == "mismatched-version") change = change with { AfterSha256 = new string('A', 64) };

        var result = await fixture.ExecuteAsync(change, context: context, approve: scenario != "denied", trusted: scenario != "untrusted");

        Assert.Equal(error, result.ErrorCode);
        Assert.Null(result.FileChanges);
        Assert.Equal("agent update\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Theory]
    [InlineData("before-missing")]
    [InlineData("before-hash")]
    [InlineData("after-missing")]
    [InlineData("length")]
    [InlineData("other-task")]
    public async Task InvalidHistory_IsNeverReplacedByLiveContent(string damage)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync();
        var context = fixture.Context;
        var beforePath = System.IO.Path.Combine(context.ArtifactDirectory!, change.BeforeSnapshot!.FileName);
        if (damage == "before-missing") File.Delete(beforePath);
        else if (damage == "before-hash") await File.WriteAllBytesAsync(beforePath, Encoding.UTF8.GetBytes("tampered\r\n"), fixture.Token);
        else if (damage == "after-missing") File.Delete(System.IO.Path.Combine(context.ArtifactDirectory!, change.AfterSnapshot!.FileName));
        else if (damage == "length") change = change with { BeforeSnapshot = change.BeforeSnapshot with { Length = 1 } };
        else context = context with { TaskId = Guid.NewGuid(), ArtifactDirectory = System.IO.Path.Combine(fixture.Root, "other-task") };

        var result = await fixture.ExecuteAsync(change, context: context);

        Assert.Equal("snapshot_invalid", result.ErrorCode);
        Assert.Equal("agent update\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        Assert.Null(result.FileChanges);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ExternallyChangedFiles_AreNeitherOverwrittenNorDeleted(bool create, bool missing)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: create);
        if (missing) File.Delete(fixture.Path);
        else await File.WriteAllTextAsync(fixture.Path, "external sentinel", fixture.Token);

        var result = await fixture.ExecuteAsync(change, remove: create);

        Assert.Equal("file_changed", result.ErrorCode);
        if (missing) Assert.False(File.Exists(fixture.Path));
        else Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
        Assert.Null(result.FileChanges);
    }

    [Fact]
    public async Task DeletedVersion_DoesNotOverwriteAnExternallyRecreatedFileEvenWithAFreshCache()
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: true);
        var removed = Assert.Single((await fixture.ExecuteAsync(change, remove: true)).FileChanges!);
        await File.WriteAllTextAsync(fixture.Path, "external sentinel", fixture.Token);
        var context = fixture.Context with { FileSnapshots = new TaskFileSnapshotStore(fixture.Context.TaskId) };

        var result = await fixture.ExecuteAsync(removed, context: context);

        Assert.Equal("file_changed", result.ErrorCode);
        Assert.Equal("external sentinel", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
    }

    [Fact]
    public async Task FullAccess_DoesNotBypassHighRiskCreatedFileDeletion()
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: true);

        var result = await fixture.ExecuteAsync(change, remove: true, context: fixture.Context);

        Assert.Equal("approval_denied", result.ErrorCode);
        Assert.True(File.Exists(fixture.Path));
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheWrongMutationKind_IsRejectedBeforeApproval(bool create)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: create);

        var result = await fixture.ExecuteAsync(change, remove: !create);

        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
        Assert.Equal("agent update\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationBeforeExecution_LeavesTheFileUntouched(bool create)
    {
        using var fixture = new FileChangeRestoreFixture();
        var change = await fixture.ChangeAsync(create: create);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancellation.Cancel();

        var result = await fixture.ExecuteAsync(change, remove: create, token: cancellation.Token);

        Assert.Equal("tool_cancelled", result.ErrorCode);
        Assert.Equal("agent update\r\n", await File.ReadAllTextAsync(fixture.Path, fixture.Token));
    }

    [Fact]
    public async Task ReplacedTargetJunction_IsRejectedWithoutTouchingTheOutsideTree()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var fixture = new FileChangeRestoreFixture();
        using var outside = new FileEditingFixture();
        var change = await fixture.ChangeAsync(create: true);
        await outside.SeedAsync("outside sentinel");
        File.Delete(fixture.Path);
        await TestDirectoryJunction.CreateAsync(fixture.Path, outside.Root, fixture.Token);
        try
        {
            var result = await fixture.ExecuteAsync(change, remove: true);
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(0, fixture.ApprovalRequests);
            Assert.Equal("outside sentinel", await outside.ContentsAsync());
        }
        finally { Directory.Delete(fixture.Path); }
    }
}
