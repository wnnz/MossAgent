using System.Text;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FilePreviewToolTests
{
    [Fact]
    public async Task Pipeline_ValidatesSchemaApprovalAndCancellation()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("preview");
        var tool = new FilePreviewTool(fixture.Paths);
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PreviewDenyApprovalService());
        var context = fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
        var denied = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = fixture.Path }), context, fixture.Token);
        Assert.Equal("approval_denied", denied.ErrorCode);
        var invalid = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = fixture.Path, unknown = true }), context, fixture.Token);
        Assert.Equal("invalid_arguments", invalid.ErrorCode);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        cancellation.Cancel();
        var cancelled = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = fixture.Path }), context, cancellation.Token);
        Assert.Equal("tool_cancelled", cancelled.ErrorCode);
    }

    [Fact]
    public async Task Preview_ProducesBoundedSnapshotWithoutPuttingFileIntoModelOutput()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync("public class Preview { }\n");
        var context = fixture.Context with { ArtifactDirectory = System.IO.Path.Combine(fixture.Root, "artifacts") };
        var result = await fixture.ExecuteAsync(new FilePreviewTool(fixture.Paths), new { path = fixture.Path }, context);
        Assert.True(result.IsSuccess);
        Assert.Null(result.Content);
        var artifact = Assert.Single(result.Artifacts!);
        Assert.Equal("text/plain", artifact.MediaType);
        Assert.Equal(await fixture.ContentsAsync(), await File.ReadAllTextAsync(artifact.Path, fixture.Token));
        Assert.Equal(64, artifact.Sha256.Length);
    }

    [Fact]
    public async Task OutsideAuthorizedRoots_IsRejectedBeforeReading()
    {
        using var fixture = new FileEditingFixture();
        var result = await fixture.ExecuteAsync(new FilePreviewTool(fixture.Paths), new { path = "../outside.txt" });
        Assert.False(result.IsSuccess);
        Assert.Equal("path_denied", result.ErrorCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task BinaryOrOversizedText_IsRejectedWithoutArtifact(bool binary)
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync(binary ? "unsupported\0binary" : new string('a', FilePreviewFormat.MaximumTextBytes + 1));
        var result = await fixture.ExecuteAsync(new FilePreviewTool(fixture.Paths), new { path = fixture.Path });
        Assert.Equal("preview_unsupported", result.ErrorCode);
        Assert.Null(result.Artifacts);
    }

    [Fact]
    public async Task OversizedFile_IsRejectedBeforeAllocation()
    {
        using var fixture = new FileEditingFixture();
        await fixture.SeedAsync(new string('a', FilePreviewFormat.MaximumBytes + 1));
        var result = await fixture.ExecuteAsync(new FilePreviewTool(fixture.Paths), new { path = fixture.Path });
        Assert.Equal("preview_too_large", result.ErrorCode);
    }

    [Fact]
    public void Format_RejectsUnsafeDimensionsMalformedImagesAndNonUtf8()
    {
        var png = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(16, 4), 8192);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(png.AsSpan(20, 4), 8192);
        Assert.Throws<InvalidDataException>(() => FilePreviewFormat.Detect(png));
        Assert.Throws<InvalidDataException>(() => FilePreviewFormat.Detect(new byte[] { 255, 216, 255 }));
        Assert.Throws<DecoderFallbackException>(() => FilePreviewFormat.Detect(new byte[] { 254, 253 }));
    }
}
