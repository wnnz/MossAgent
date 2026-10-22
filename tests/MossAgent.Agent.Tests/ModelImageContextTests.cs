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
    public void SharedChildContext_KeepsTextFreeOfBinaryAndExposesBoundedImageInputs(string text)
    {
        var image = ModelImage.FromBytes("image/png", new byte[128]);

        var context = SubagentConversationContextBuilder.Build([new(ModelRole.User, text, Images: [image])]);
        var shared = SubagentConversationContextBuilder.BuildImages(
            [new(ModelRole.User, text, Images: [image])]);

        Assert.NotNull(context);
        Assert.Contains("Image inputs are attached separately", context);
        Assert.DoesNotContain(image.Base64Data, context);
        var attachment = Assert.Single(shared);
        Assert.Equal(image.MediaType, attachment.MediaType);
        Assert.Equal(image.ByteCount, attachment.ByteCount);
        Assert.Equal(image.Base64Data, attachment.Base64Data);
        if (text.Length > 0) Assert.Contains(text, context);
    }

    [Fact]
    public void SharedChildImagesUseLatestTenUserImagesAcrossRoles()
    {
        var images = Enumerable.Range(0, 12)
            .Select(index => ModelImage.FromBytes("image/png", [(byte)(index + 1)]))
            .ToArray();
        var messages = new List<ModelMessage>();
        foreach (var image in images.Take(11))
            messages.Add(new(ModelRole.User, "image", Images: [image]));
        messages.Add(new(ModelRole.Assistant, "ignored", Images: [images[11]]));

        var selected = SubagentConversationContextBuilder.BuildImages(messages);

        Assert.Equal(ModelImageRequestValidation.MaximumImages, selected.Length);
        Assert.Equal(images[1].Base64Data, selected[0].Base64Data);
        Assert.Equal(images[10].Base64Data, selected[^1].Base64Data);
    }

    [Fact]
    public void SharedChildImagesStayInsideTheRecentContextMessageWindow()
    {
        var image = ModelImage.FromBytes("image/png", [1]);
        var messages = new List<ModelMessage>
        {
            new(ModelRole.User, "old image", Images: [image])
        };
        for (var index = 0; index < 12; index++)
            messages.Add(new(ModelRole.User, $"recent {index}"));

        Assert.True(SubagentConversationContextBuilder.BuildImages(messages).IsEmpty);
    }

    private static ModelProfile Model() => new(Guid.NewGuid(), Guid.NewGuid(), "fixture", "fixture",
        1_024, 128, "off", null, null, true, false, true, true, "{}");
}
