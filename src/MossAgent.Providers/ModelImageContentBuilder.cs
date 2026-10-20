using System.Text.Json.Nodes;
using MossAgent.Application.Models;

namespace MossAgent.Providers;

internal static class ModelImageContentBuilder
{
    public static JsonNode Responses(ModelMessage message) =>
        Build(message, "input_text", static image => new JsonObject
        {
            ["type"] = "input_image",
            ["image_url"] = DataUrl(image),
            ["detail"] = "auto"
        });

    public static JsonNode Compatible(ModelMessage message) =>
        Build(message, "text", static image => new JsonObject
        {
            ["type"] = "image_url",
            ["image_url"] = new JsonObject { ["url"] = DataUrl(image), ["detail"] = "auto" }
        });

    public static JsonNode Anthropic(ModelMessage message) =>
        Build(message, "text", static image => new JsonObject
        {
            ["type"] = "image",
            ["source"] = new JsonObject
            {
                ["type"] = "base64", ["media_type"] = image.MediaType, ["data"] = image.Base64Data
            }
        });

    public static void AppendGemini(JsonArray parts, ModelMessage message)
    {
        if (message.Images.IsDefaultOrEmpty) return;
        foreach (var image in message.Images)
            parts.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = image.MediaType, ["data"] = image.Base64Data
                }
            });
    }

    private static JsonNode Build(
        ModelMessage message, string textType, Func<ModelImage, JsonObject> imageBlock)
    {
        if (message.Images.IsDefaultOrEmpty) return JsonValue.Create(message.Content)!;
        var blocks = new JsonArray();
        if (!string.IsNullOrEmpty(message.Content))
            blocks.Add(new JsonObject { ["type"] = textType, ["text"] = message.Content });
        foreach (var image in message.Images) blocks.Add(imageBlock(image));
        return blocks;
    }

    private static string DataUrl(ModelImage image) => $"data:{image.MediaType};base64,{image.Base64Data}";
}
