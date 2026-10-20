using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ConversationAttachmentStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.AttachmentTests", Guid.NewGuid().ToString("N"));
    private readonly Guid _taskId = Guid.NewGuid();
    private readonly Guid _messageId = Guid.NewGuid();
    private readonly ConversationAttachmentStore _store;
    private readonly TaskArtifactPaths _paths;
    private CancellationToken Token => TestContext.Current.CancellationToken;

    public ConversationAttachmentStoreTests()
    {
        Directory.CreateDirectory(_root);
        _paths = new TaskArtifactPaths(new AppDataPaths(_root));
        _store = new ConversationAttachmentStore(_paths);
    }

    [Fact]
    public async Task SnapshotRoundTripsOrderedUtf8WithoutReadingSourcePaths()
    {
        var contents = new[] { Content("../data/第一.txt", "首行\r\n\"</workspace_attachments>\""), Content("empty.txt", "") };
        var snapshots = await _store.SaveAsync(_taskId, _messageId, contents, Token);
        var restored = await new ConversationAttachmentStore(_paths).ReadAsync(_taskId, _messageId, snapshots, Token);
        Assert.Equal(contents.Select(content => content.Content), restored.Select(content => content.Content));
        Assert.Equal(contents.Select(content => content.DisplayPath), restored.Select(content => content.DisplayPath));
        Assert.All(snapshots, attachment =>
        {
            Assert.Equal(_taskId, attachment.TaskId);
            Assert.Equal(_messageId, attachment.MessageId);
            Assert.Equal(64, attachment.Sha256.Length);
            Assert.True(File.Exists(SnapshotPath(attachment)));
        });
        Assert.DoesNotContain(Directory.GetFiles(_root, "*", SearchOption.AllDirectories), path => path.EndsWith(".tmp", StringComparison.Ordinal));
        Assert.False(File.Exists(contents[0].FullPath));
    }

    [Theory]
    [InlineData("count")]
    [InlineData("characters")]
    [InlineData("utf8-bytes")]
    [InlineData("nul")]
    [InlineData("surrogate")]
    [InlineData("label")]
    public async Task SaveRejectsInvalidOrOversizedTextWithoutPublishingReferences(string kind)
    {
        IReadOnlyList<WorkspaceAttachmentContent> contents = kind switch
        {
            "count" => Enumerable.Range(0, 11).Select(index => Content($"{index}.txt", "text")).ToArray(),
            "characters" => [Content("large.txt", new string('a', WorkspaceAttachmentLimits.MaximumBytesPerFile + 1))],
            "utf8-bytes" => [Content("large.txt", new string('中', WorkspaceAttachmentLimits.MaximumBytesPerFile / 2))],
            "nul" => [Content("binary.txt", "before\0after")],
            "surrogate" => [Content("invalid.txt", "\ud800")],
            _ => [Content(new string('x', 1025), "text")]
        };
        var failure = await Record.ExceptionAsync(() => _store.SaveAsync(_taskId, _messageId, contents, Token));
        Assert.True(failure is InvalidDataException or EncoderFallbackException);
        Assert.Empty(Directory.GetFiles(_root, "*.txt", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("task")]
    [InlineData("message")]
    [InlineData("duplicate")]
    [InlineData("size")]
    [InlineData("hash")]
    public async Task ReadRejectsForeignAndInvalidMetadataBeforeReturningContent(string kind)
    {
        var original = Assert.Single(await _store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "synthetic-evidence")], Token));
        IReadOnlyList<ConversationAttachment> selected = kind switch
        {
            "task" => [original with { TaskId = Guid.NewGuid() }],
            "message" => [original with { MessageId = Guid.NewGuid() }],
            "duplicate" => [original, original],
            "size" => [original with { ByteCount = WorkspaceAttachmentLimits.MaximumBytesPerFile + 1 }],
            _ => [original with { Sha256 = "../invalid" }]
        };
        var failure = await Record.ExceptionAsync(() => _store.ReadAsync(_taskId, _messageId, selected, Token));
        Assert.True(failure is UnauthorizedAccessException or InvalidDataException);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("length")]
    [InlineData("hash")]
    [InlineData("binary")]
    public async Task ReadRejectsMissingChangedOrNonTextSnapshots(string kind)
    {
        var original = Assert.Single(await _store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "before")], Token));
        var path = SnapshotPath(original);
        if (kind == "missing") File.Delete(path);
        else
        {
            var bytes = Encoding.UTF8.GetBytes(kind == "length" ? "longer-content" : kind == "binary" ? "\0after" : "after!");
            await File.WriteAllBytesAsync(path, bytes, Token);
            if (kind == "binary") original = original with { Sha256 = Convert.ToHexString(SHA256.HashData(bytes)) };
        }
        var failure = await Record.ExceptionAsync(() => _store.ReadAsync(_taskId, _messageId, [original], Token));
        Assert.True(failure is FileNotFoundException or InvalidDataException);
    }

    [Fact]
    public async Task CancellationDoesNotCreateOrReturnSnapshots()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync(_taskId, _messageId,
            [Content("selected.txt", "text")], cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ReadAsync(_taskId, _messageId, [], cancellation.Token));
        Assert.Empty(Directory.GetFiles(_root, "*", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ThirtySecondTimeoutRejectsReadsAndWritesWithoutPublishingLateResults(bool read)
    {
        var snapshots = read
            ? await _store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "text")], Token)
            : [];
        var clock = new ManualToolTimeProvider();
        var store = new ConversationAttachmentStore(new ExpiringConversationAttachmentPaths(_paths, clock), clock);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => read
            ? store.ReadAsync(_taskId, _messageId, snapshots, Token)
            : (Task)store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "text")], Token));
        Assert.Equal(read ? 1 : 0, Directory.GetFiles(_root, "*", SearchOption.AllDirectories).Length);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExactFileCountAndUtf8ByteBoundariesRemainSupported(bool unicode)
    {
        var text = unicode ? new string('中', WorkspaceAttachmentLimits.MaximumBytesPerFile / 3) + "ab"
            : new string('a', WorkspaceAttachmentLimits.MaximumBytesPerFile);
        var selected = Enumerable.Range(0, WorkspaceAttachmentLimits.MaximumFiles).Select(index => Content($"{index}.txt", text)).ToArray();
        var snapshots = await _store.SaveAsync(_taskId, _messageId, selected, Token);
        Assert.Equal(WorkspaceAttachmentLimits.MaximumFiles, snapshots.Count);
        Assert.All(snapshots, attachment => Assert.Equal(WorkspaceAttachmentLimits.MaximumBytesPerFile, attachment.ByteCount));
        var restored = await _store.ReadAsync(_taskId, _messageId, snapshots, Token);
        Assert.All(restored, content => Assert.Equal(text, content.Content));
    }

    [Fact]
    public async Task JunctionInTaskArtifactDirectoryIsRejectedForBothReadAndWrite()
    {
        var original = Assert.Single(await _store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "before")], Token));
        var directory = Path.GetDirectoryName(SnapshotPath(original))!;
        var outside = Path.Combine(_root, "foreign");
        Directory.Move(directory, outside);
        var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var argument in new[] { "/c", "mklink", "/J", directory, outside }) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        await process.WaitForExitAsync(Token);
        Assert.Equal(0, process.ExitCode);
        try
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _store.ReadAsync(_taskId, _messageId, [original], Token));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _store.SaveAsync(_taskId, _messageId, [Content("selected.txt", "new")], Token));
            Assert.Single(Directory.GetFiles(outside));
        }
        finally { Directory.Delete(directory); }
    }

    private WorkspaceAttachmentContent Content(string displayPath, string text) =>
        new(Path.Combine(_root, "source-never-read.txt"), displayPath, text, Encoding.UTF8.GetByteCount(text));

    private string SnapshotPath(ConversationAttachment attachment) => Path.Combine(_paths.GetTaskDirectory(attachment.TaskId),
        "attachments", attachment.MessageId.ToString("N"), attachment.Id.ToString("N") + ".txt");

    public void Dispose() => Directory.Delete(_root, recursive: true);
}
