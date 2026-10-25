using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Agent.Tests;

public sealed class ModelImageContextTests
{
    [Fact]
    public void Estimate_IncludesImagesAsIndivisibleMetadata()
    {
        var image = ModelImage.FromBytes("image/png", new byte[900]);
        var plain = new ModelMessage(ModelRole.User, "text");

        Assert.Equal(image.ContextCharacterCost,
            ModelContextWindowBuilder.Estimate([plain with { Images = [image] }]) - ModelContextWindowBuilder.Estimate([plain]));
    }

    [Fact]
    public void OversizedLatestImage_FailsInsteadOfSlicingOrSilentlyDroppingPixels()
    {
        var image = ModelImage.FromBytes("image/png", new byte[6_000]);
        var message = new ModelMessage(ModelRole.User, "describe", Images: [image]);

        Assert.Throws<InvalidOperationException>(() => ModelContextWindowBuilder.Build([message], [], Model()));
        Assert.Equal(6_000, Convert.FromBase64String(image.Base64Data).Length);
    }

    [Fact]
    public void OversizedOldImage_EvictsItsWholeTurnWithoutSkippingOverIt()
    {
        var image = ModelImage.FromBytes("image/png", new byte[6_000]);
        var latest = new ModelMessage(ModelRole.User, "latest");
        ModelMessage[] messages = [new(ModelRole.User, "oldest"),
            new(ModelRole.User, "old image", Images: [image]), new(ModelRole.Assistant, "old answer"), latest];

        var result = ModelContextWindowBuilder.Build(messages, [], Model());

        Assert.Same(latest, Assert.Single(result));
    }

    [Fact]
    public void LatestTextCanBeTrimmed_WithoutChangingItsImages()
    {
        var image = ModelImage.FromBytes("image/png", new byte[68]);
        var original = new ModelMessage(ModelRole.User, new string('x', 10_000), Images: [image]);

        var result = Assert.Single(ModelContextWindowBuilder.Build([original], [], Model()));

        Assert.Same(image, Assert.Single(result.Images));
        Assert.True(result.Content.Length < original.Content.Length);
        Assert.InRange(ModelContextWindowBuilder.Estimate([result]), image.ContextCharacterCost, 1_536);
        Assert.Equal(10_000, original.Content.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("describe this")]
    public void SharedChildContext_CarriesImagesAndMarksTheirCount(string text)
    {
        var image = ModelImage.FromBytes("image/png", new byte[128]);

        var context = SubagentConversationContextBuilder.Build([new(ModelRole.User, text, Images: [image])]);

        Assert.NotNull(context);
        Assert.Contains("[包含 1 张图像输入]", context.Text);
        Assert.DoesNotContain(image.Base64Data, context.Text);
        var shared = Assert.Single(context.Images);
        Assert.Equal(image.MediaType, shared.MediaType);
        Assert.Equal(image.ByteCount, shared.ByteCount);
        Assert.Equal(image.Base64Data, shared.Base64Data);
        if (text.Length > 0) Assert.Contains(text, context.Text);
    }

    private static ModelProfile Model() => new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "fixture",
        1_024, 128, "off", null, null, true, false, true, true, "{}");
}
