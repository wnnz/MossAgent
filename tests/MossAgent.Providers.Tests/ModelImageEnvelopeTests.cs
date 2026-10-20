using System.Text.Json;
using MossAgent.Application.Models;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelImageEnvelopeTests
{
    [Fact]
    public void Envelope_OwnsAnImmutableCopyOfCallerBytes()
    {
        var bytes = Convert.FromBase64String(ModelImageTestFixture.RedPng);
        var image = ModelImage.FromBytes("image/png", bytes);
        Array.Clear(bytes);

        Assert.Equal(ModelImageTestFixture.RedPng, image.Base64Data);
        Assert.Equal(69, image.ByteCount);
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/plain")]
    [InlineData("data:image/png;base64,private-pixels")]
    [InlineData("C:\\private\\image.png")]
    public void Envelope_RejectsUnsupportedMediaWithoutEchoingIt(string mediaType)
    {
        var error = Assert.Throws<ArgumentException>(() => ModelImage.FromBytes(mediaType, [1]));

        Assert.DoesNotContain(mediaType, error.Message);
    }

    [Fact]
    public void Envelope_EnforcesExactByteBoundaries()
    {
        Assert.Throws<ArgumentException>(() => ModelImage.FromBytes("image/png", []));
        Assert.Equal(ModelImage.MaximumByteCount,
            ModelImage.FromBytes("image/png", new byte[ModelImage.MaximumByteCount]).ByteCount);
        Assert.Throws<ArgumentException>(() => ModelImage.FromBytes("image/png", new byte[ModelImage.MaximumByteCount + 1]));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    public void Envelope_DoesNotPretendToBeAFileDecoder(string mediaType)
    {
        var envelope = ModelImage.FromBytes(mediaType, [1, 2, 3]);

        Assert.Equal(mediaType, envelope.MediaType);
        Assert.Equal("AQID", envelope.Base64Data);
    }

    [Fact]
    public void OrdinarySerializationAndDiagnostics_ExcludePixels()
    {
        var image = ModelImageTestFixture.Image();
        var message = new ModelMessage(ModelRole.User, "text", Images: [image]);

        Assert.DoesNotContain(image.Base64Data, image.ToString());
        Assert.DoesNotContain(image.Base64Data, message.ToString());
        using var envelopeJson = JsonDocument.Parse(JsonSerializer.Serialize(image));
        Assert.False(envelopeJson.RootElement.TryGetProperty("Base64Data", out _));
        Assert.False(envelopeJson.RootElement.TryGetProperty("ContextCharacterCost", out _));
        using var messageJson = JsonDocument.Parse(JsonSerializer.Serialize(message));
        Assert.False(messageJson.RootElement.TryGetProperty("Images", out _));
        Assert.DoesNotContain(image.Base64Data, messageJson.RootElement.GetRawText());
    }
}
