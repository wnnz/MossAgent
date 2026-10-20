using System.Diagnostics;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using Xunit;
using static MossAgent.Infrastructure.Tests.ConversationDraftAttachmentTestData;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteConversationDraftAttachmentBoundaryTests
{
    [Theory]
    [InlineData("projectless")]
    [InlineData("duplicate")]
    [InlineData("count")]
    [InlineData("identity")]
    [InlineData("path")]
    [InlineData("label")]
    [InlineData("stamp")]
    [InlineData("bytes")]
    [InlineData("utf8")]
    [InlineData("binary")]
    public async Task InvalidAttachmentBatchCannotPublishAnyTextOrMetadata(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(kind == "projectless" ? null : fixture.Project.Id, null);
        var attachment = Attachment(fixture);
        var items = kind switch
        {
            "duplicate" => new[] { attachment, attachment },
            "count" => Enumerable.Range(0, 11).Select(_ => Attachment(fixture)).ToArray(),
            "identity" => [attachment with { Id = Guid.Empty }],
            "path" => [attachment with { FullPath = "../foreign.txt" }],
            "label" => [attachment with { DisplayPath = new string('x', 1025) }],
            "stamp" => [attachment with { AuthorizationStamp = "../foreign" }],
            "bytes" => [attachment with { ByteCount = WorkspaceAttachmentLimits.MaximumBytesPerFile + 1L }],
            "utf8" => [attachment with { Content = "\ud800", ByteCount = 3 }],
            "binary" => [attachment with { Content = "a\0b", ByteCount = 3 }],
            _ => [attachment]
        };
        var failure = await Record.ExceptionAsync(() => Repository(fixture).SaveAsync([new(scope, "must not save", 0, items)], fixture.Token));
        if (kind == "projectless") Assert.IsType<UnauthorizedAccessException>(failure);
        else Assert.True(failure is ArgumentException or InvalidDataException);
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_drafts;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
    }

    [Theory]
    [InlineData("foreign")]
    [InlineData("missing")]
    [InlineData("archived")]
    public async Task InvalidTaskOwnershipCannotReadOrPublishSnapshots(string kind)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "owned", 0, [attachment])], fixture.Token));
        if (kind == "archived") await fixture.Repository.SetTaskArchivedAsync(fixture.Task.Id, true, fixture.Token);
        var invalid = kind == "foreign" ? scope with { ProjectId = Guid.NewGuid() }
            : kind == "missing" ? scope with { TaskId = Guid.NewGuid() } : scope;
        Assert.Null(await repository.GetAsync(invalid, fixture.Token));
        Assert.False(await repository.SaveAsync([new(invalid, "intruder", 1, [Attachment(fixture)])], fixture.Token));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
        Assert.Single(Directory.GetFiles(Path.Combine(fixture.Project.PrimaryDirectory, "draft-attachments"), "*.txt", SearchOption.AllDirectories));
    }

    [Fact]
    public async Task ExactUtf8SizeAndFileCountRemainSupported()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var text = new string('中', WorkspaceAttachmentLimits.MaximumBytesPerFile / 3) + "ab";
        var attachments = Enumerable.Range(0, WorkspaceAttachmentLimits.MaximumFiles).Select(_ => Attachment(fixture, text)).ToArray();
        Assert.True(await Repository(fixture).SaveAsync([new(scope, "bounded", 0, attachments)], fixture.Token));
        var saved = (await Repository(fixture).GetAsync(scope, fixture.Token))!;
        Assert.Equal(WorkspaceAttachmentLimits.MaximumFiles, saved.AttachmentItems.Count);
        Assert.All(saved.AttachmentItems, attachment => Assert.Equal(text, attachment.Content));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CancellationAndThirtySecondTimeoutNeverPublishAnUpdate(bool timeout)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var attachment = Attachment(fixture);
        var clock = timeout ? new ExpiringDraftTimeProvider() : null;
        var repository = Repository(fixture, clock);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        if (!timeout) await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.SaveAsync([new(scope, "cancelled", 0, [attachment])], cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAsync(scope, cancellation.Token));
        if (clock is not null) Assert.Equal(TimeSpan.FromSeconds(30), clock.ObservedTimeout);
        Assert.Null(await Repository(fixture).GetAsync(scope, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM conversation_draft_attachments;"));
    }

    [Fact]
    public async Task SnapshotDirectoryJunctionIsUnavailableForReadAndRejectedForWrite()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var repository = Repository(fixture);
        var scope = new ConversationDraftScope(fixture.Project.Id, fixture.Task.Id);
        var original = Attachment(fixture);
        Assert.True(await repository.SaveAsync([new(scope, "owned", 0, [original])], fixture.Token));
        var directory = Path.GetFullPath(Path.GetDirectoryName(Snapshot(fixture, original))!);
        var outside = Path.GetFullPath(Path.Combine(fixture.Project.PrimaryDirectory, "foreign-snapshots"));
        var root = Path.GetFullPath(fixture.Project.PrimaryDirectory) + Path.DirectorySeparatorChar;
        Assert.StartsWith(root, directory, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith(root, outside, StringComparison.OrdinalIgnoreCase);
        Directory.Move(directory, outside);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", directory, outside }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync(fixture.Token);
        Assert.Equal(0, process.ExitCode);
        try
        {
            Assert.Null(Assert.Single((await repository.GetAsync(scope, fixture.Token))!.AttachmentItems).Content);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => repository.SaveAsync([new(scope, "must roll back", 1, [original])], fixture.Token));
            Assert.Equal("owned", (await repository.GetAsync(scope, fixture.Token))!.Text);
            Assert.Single(Directory.GetFiles(outside));
        }
        finally { Directory.Delete(directory); }
    }
}
