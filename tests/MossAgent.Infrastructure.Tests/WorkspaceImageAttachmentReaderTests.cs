using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorkspaceImageAttachmentReaderTests
{
    [Fact]
    public async Task ActualMediaDetectionSupportsMultipleAuthorizedRootsAndRejectsTraversalToSibling()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var primary = Path.Combine(fixture.Project.PrimaryDirectory, "primary");
        var secondary = Path.Combine(fixture.Project.PrimaryDirectory, "secondary");
        Directory.CreateDirectory(primary);
        Directory.CreateDirectory(secondary);
        var project = fixture.Project with { PrimaryDirectory = primary, AuthorizedDirectories = [secondary] };
        var paths = new[] { Path.Combine(primary, "renamed.dat"), Path.Combine(secondary, "renamed.txt") };
        foreach (var path in paths) await File.WriteAllBytesAsync(path, AttachmentImageTestData.Static("image/png"), fixture.Token);
        var reader = new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder());
        var selected = await reader.ReadAsync(project, paths, fixture.Token);
        Assert.Equal(2, selected.Count);
        Assert.All(selected, item => Assert.Equal("image/png", item.MediaType));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync(project,
            [Path.Combine(primary, "..", "outside.png")], fixture.Token));
        var fake = Path.Combine(primary, "fake.png");
        await File.WriteAllTextAsync(fake, "not actually an image", fixture.Token);
        await Assert.ThrowsAsync<AttachmentImageValidationException>(() => reader.ReadAsync(project, [fake], fixture.Token));
    }

    [Fact]
    public async Task EncodedImageLimitAndAggregateLimitAreIndependentOfTheTextLimit()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var bytes = AttachmentImageTestData.Static("image/jpeg");
        Array.Resize(ref bytes, AttachmentImage.MaximumByteCount);
        var paths = Enumerable.Range(0, 3).Select(index => Path.Combine(fixture.Project.PrimaryDirectory, $"image-{index}.jpg")).ToArray();
        foreach (var path in paths) await File.WriteAllBytesAsync(path, bytes, fixture.Token);
        var reader = new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder());
        var accepted = await reader.ReadAsync(fixture.Project, paths[..2], fixture.Token);
        Assert.Equal(AttachmentImage.MaximumTotalByteCount, accepted.Sum(item => item.ByteCount));
        var exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() => reader.ReadAsync(fixture.Project, paths, fixture.Token));
        Assert.Equal(AttachmentImageFailure.SizeLimit, exception.Failure);
        await using (var stream = new FileStream(paths[0], FileMode.Open, FileAccess.Write)) stream.SetLength(AttachmentImage.MaximumByteCount + 1L);
        exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() => reader.ReadAsync(fixture.Project, [paths[0]], fixture.Token));
        Assert.Equal(AttachmentImageFailure.SizeLimit, exception.Failure);
    }

    [Fact]
    public async Task CancellationAndMissingDecoderCannotReturnImageResults()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var path = Path.Combine(fixture.Project.PrimaryDirectory, "selected.png");
        await File.WriteAllBytesAsync(path, AttachmentImageTestData.Static("image/png"), fixture.Token);
        var exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new WorkspaceAttachmentReader().ReadAsync(fixture.Project, [path], fixture.Token));
        Assert.Equal(AttachmentImageFailure.DecoderUnavailable, exception.Failure);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new WorkspaceAttachmentReader(new SkiaAttachmentImageDecoder()).ReadAsync(fixture.Project, [path], cancellation.Token));
    }
}
