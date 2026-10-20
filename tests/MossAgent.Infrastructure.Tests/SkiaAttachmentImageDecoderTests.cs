using System.Text.Json;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SkiaAttachmentImageDecoderTests
{
    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task RealStaticCodecsRetainImmutableBytesWithoutOrdinaryJsonOrDiagnosticPixels(string mediaType)
    {
        var bytes = AttachmentImageTestData.Static(mediaType);
        var expected = Convert.ToBase64String(bytes);
        var decoding = new SkiaAttachmentImageDecoder().DecodeAsync(bytes, TestContext.Current.CancellationToken);
        Array.Fill<byte>(bytes, 0);
        var image = await decoding;
        Assert.Equal(mediaType, image.MediaType);
        Assert.Equal(expected, image.Base64Data);
        Assert.Equal(bytes.Length, image.ByteCount);
        Assert.Equal(1, image.Width);
        Assert.Equal(1, image.Height);
        Assert.DoesNotContain(expected, JsonSerializer.Serialize(image));
        Assert.DoesNotContain(expected, image.ToString());
    }

    [Theory]
    [InlineData(AttachmentImageTestData.AnimatedGif)]
    [InlineData(AttachmentImageTestData.AnimatedPng)]
    [InlineData(AttachmentImageTestData.AnimatedWebp)]
    public async Task ActualAnimatedImagesAreRejectedInsteadOfSilentlySelectingTheirFirstFrame(string encoded)
    {
        var exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new SkiaAttachmentImageDecoder().DecodeAsync(Convert.FromBase64String(encoded), TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentImageFailure.Animated, exception.Failure);
        Assert.DoesNotContain(encoded, exception.ToString());
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public async Task SignatureAloneAndTruncatedPixelsDoNotCreateAnImageSnapshot(string mediaType)
    {
        var bytes = AttachmentImageTestData.Static(mediaType);
        foreach (var length in new[] { Math.Min(12, bytes.Length), bytes.Length / 2 })
            await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
                new SkiaAttachmentImageDecoder().DecodeAsync(bytes.AsMemory(0, length), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(8193, 1)]
    [InlineData(1, 8193)]
    [InlineData(4097, 4097)]
    public async Task DimensionsAndPixelCountAreCheckedBeforeAllocatingDecodedPixels(int width, int height)
    {
        var exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() =>
            new SkiaAttachmentImageDecoder().DecodeAsync(AttachmentImageTestData.Dimensions(width, height), TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentImageFailure.SizeLimit, exception.Failure);
    }

    [Fact]
    public async Task EncodedByteLimitUnsupportedContentAndCancellationFailClosed()
    {
        var decoder = new SkiaAttachmentImageDecoder();
        var tooLarge = new byte[AttachmentImage.MaximumByteCount + 1];
        var exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() => decoder.DecodeAsync(tooLarge, TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentImageFailure.SizeLimit, exception.Failure);
        exception = await Assert.ThrowsAsync<AttachmentImageValidationException>(() => decoder.DecodeAsync("not an image"u8.ToArray(), TestContext.Current.CancellationToken));
        Assert.Equal(AttachmentImageFailure.Unsupported, exception.Failure);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decoder.DecodeAsync(AttachmentImageTestData.Static("image/png"), cancellation.Token));
    }
}
