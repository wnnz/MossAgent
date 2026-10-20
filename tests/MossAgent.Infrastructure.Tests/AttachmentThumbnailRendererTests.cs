using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using SkiaSharp;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentThumbnailRendererTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task SupportedImagesProduceBoundedStaticPngWithoutChangingTheInput(string mediaType)
    {
        var bytes = AttachmentImageTestData.Static(mediaType);
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(bytes, Token);

        var result = await new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token);

        Assert.Equal("image/png", result.MediaType);
        Assert.Equal(1, result.Width);
        Assert.Equal(1, result.Height);
        Assert.InRange(result.ByteCount, 1, IAttachmentThumbnailRenderer.MaximumByteCount);
        Assert.Equal(bytes, Convert.FromBase64String(image.Base64Data));
        using var decoded = SKBitmap.Decode(Convert.FromBase64String(result.Base64Data));
        Assert.NotNull(decoded);
        Assert.Equal(new SKSizeI(1, 1), decoded.Info.Size);
    }

    [Theory]
    [InlineData(480, 240, 96, 48)]
    [InlineData(240, 480, 48, 96)]
    [InlineData(64, 32, 64, 32)]
    [InlineData(8192, 1, 96, 1)]
    public async Task AspectRatioIsRetainedAndSmallInputsAreNotUpscaled(int width, int height, int expectedWidth, int expectedHeight)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.Red);
        using var source = SKImage.FromBitmap(bitmap);
        using var data = source.Encode(SKEncodedImageFormat.Png, 100);
        var image = AttachmentImage.FromDecodedBytes("image/png", data.ToArray(), width, height);

        var result = await new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token);

        Assert.Equal(expectedWidth, result.Width);
        Assert.Equal(expectedHeight, result.Height);
        Assert.InRange(result.ByteCount, 1, IAttachmentThumbnailRenderer.MaximumByteCount);
        using var decoded = SKBitmap.Decode(Convert.FromBase64String(result.Base64Data));
        Assert.Equal(new SKSizeI(expectedWidth, expectedHeight), decoded.Info.Size);
        Assert.Equal(SKColors.Red, decoded.GetPixel(expectedWidth / 2, expectedHeight / 2));
    }

    [Theory]
    [InlineData("image/png", AttachmentImageTestData.AnimatedPng)]
    [InlineData("image/gif", AttachmentImageTestData.AnimatedGif)]
    [InlineData("image/webp", AttachmentImageTestData.AnimatedWebp)]
    public async Task ForgedAnimatedEnvelopesAreRejected(string mediaType, string base64)
    {
        var image = AttachmentImage.FromDecodedBytes(mediaType, Convert.FromBase64String(base64), 1, 1);

        var error = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token));

        Assert.Equal(AttachmentImageFailure.Animated, error.Failure);
    }

    [Theory]
    [InlineData("image/jpeg", 1, 1)]
    [InlineData("image/png", 2, 1)]
    public async Task MetadataMustMatchTheActualDecodedSource(string mediaType, int width, int height)
    {
        var image = AttachmentImage.FromDecodedBytes(mediaType, Convert.FromBase64String(AttachmentImageTestData.Png), width, height);

        var error = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token));

        Assert.Equal(AttachmentImageFailure.Invalid, error.Failure);
    }

    [Fact]
    public async Task OversizedActualHeaderIsRejectedBeforePixelAllocation()
    {
        var bytes = AttachmentImageTestData.Dimensions(8192, 8192);
        var image = AttachmentImage.FromDecodedBytes("image/png", bytes, 1, 1);

        var error = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token));

        Assert.Equal(AttachmentImageFailure.SizeLimit, error.Failure);
    }

    [Fact]
    public async Task InvalidSourceDoesNotPreventTheNextValidRender()
    {
        var renderer = new SkiaAttachmentThumbnailRenderer();
        var malformed = AttachmentImage.FromDecodedBytes("image/png", new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 1, 1);
        await Assert.ThrowsAsync<AttachmentImageValidationException>(() => renderer.RenderAsync(malformed, Token));
        var valid = await new SkiaAttachmentImageDecoder().DecodeAsync(AttachmentImageTestData.Static("image/png"), Token);

        var result = await renderer.RenderAsync(valid, Token);

        Assert.Equal(1, result.Width);
    }

    [Fact]
    public async Task CancellationRejectsWorkWithoutPoisoningTheSharedRenderer()
    {
        var renderer = new SkiaAttachmentThumbnailRenderer();
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(AttachmentImageTestData.Static("image/png"), Token);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => renderer.RenderAsync(image, cancellation.Token));
        Assert.Equal(1, (await renderer.RenderAsync(image, Token)).Height);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
