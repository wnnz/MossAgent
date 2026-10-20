using MossAgent.Application.Attachments;
using MossAgent.Infrastructure.Attachments;
using SkiaSharp;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AttachmentImageOrientationTests
{
    public static TheoryData<SKEncodedOrigin, bool, bool> Cases
    {
        get
        {
            var cases = new TheoryData<SKEncodedOrigin, bool, bool>();
            foreach (var origin in Enumerable.Range(1, 8))
                foreach (var large in new[] { false, true })
                    foreach (var littleEndian in new[] { false, true })
                        cases.Add((SKEncodedOrigin)origin, large, littleEndian);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ExifOrientationIsAppliedOnlyToTheBoundedPreview(SKEncodedOrigin origin, bool large, bool littleEndian)
    {
        var bytes = AttachmentOrientationTestData.Jpeg(origin, large, littleEndian);
        using var sourceData = SKData.CreateCopy(bytes);
        using var sourceCodec = SKCodec.Create(sourceData);
        Assert.Equal(origin, sourceCodec.EncodedOrigin);
        var image = await new SkiaAttachmentImageDecoder().DecodeAsync(bytes, Token);
        Assert.Equal(large ? 300 : 60, image.Width);
        Assert.Equal(large ? 200 : 40, image.Height);

        var result = await new SkiaAttachmentThumbnailRenderer().RenderAsync(image, Token);

        var transposed = (int)origin >= 5;
        var width = large ? 96 : 60;
        var height = large ? 64 : 40;
        Assert.Equal(transposed ? height : width, result.Width);
        Assert.Equal(transposed ? width : height, result.Height);
        Assert.InRange(result.ByteCount, 1, IAttachmentThumbnailRenderer.MaximumByteCount);
        Assert.Equal(bytes, Convert.FromBase64String(image.Base64Data));
        Assert.Equal("image/jpeg", image.MediaType);
        Assert.Equal("image/png", result.MediaType);
        var output = Convert.FromBase64String(result.Base64Data);
        using var outputData = SKData.CreateCopy(output);
        using var outputCodec = SKCodec.Create(outputData);
        Assert.Equal(SKEncodedOrigin.TopLeft, outputCodec.EncodedOrigin);
        using var bitmap = SKBitmap.Decode(output);
        AssertCells(bitmap, origin, transposed);
        if (large && littleEndian) SaveEvidence(origin, bytes, output);
    }

    private static void AssertCells(SKBitmap bitmap, SKEncodedOrigin origin, bool transposed)
    {
        var expected = AttachmentOrientationTestData.ExpectedCells(origin);
        var colors = AttachmentOrientationTestData.Colors;
        var columns = transposed ? 2 : 3;
        var rows = transposed ? 3 : 2;
        for (var index = 0; index < expected.Length; index++)
        {
            var color = bitmap.GetPixel((index % columns * 2 + 1) * bitmap.Width / (columns * 2),
                (index / columns * 2 + 1) * bitmap.Height / (rows * 2));
            var target = colors[expected[index]];
            Assert.InRange(Math.Abs(color.Red - target.Red), 0, 10);
            Assert.InRange(Math.Abs(color.Green - target.Green), 0, 10);
            Assert.InRange(Math.Abs(color.Blue - target.Blue), 0, 10);
            Assert.Equal(255, color.Alpha);
        }
    }

    private static void SaveEvidence(SKEncodedOrigin origin, byte[] source, byte[] preview)
    {
        var directory = Environment.GetEnvironmentVariable("MOSSAGENT_ORIENTATION_EVIDENCE_DIRECTORY");
        if (string.IsNullOrWhiteSpace(directory)) return;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, $"orientation-{(int)origin}-source.jpg"), source);
        File.WriteAllBytes(Path.Combine(directory, $"orientation-{(int)origin}-thumbnail.png"), preview);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;
}
