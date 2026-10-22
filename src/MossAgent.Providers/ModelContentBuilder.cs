using System.Text.Json.Nodes;
using MossAgent.Application.Models;

namespace MossAgent.Providers;

internal static class ModelContentBuilder
{
    public static JsonNode Responses(ModelMessage message) =>
        Build(message, "input_text", static image => new JsonObject
        {
            ["type"] = "input_image",
            ["image_url"] = DataUrl(image),
            ["detail"] = "auto"
        }, static file => new JsonObject
        {
            ["type"] = "input_file",
            ["filename"] = file.FileName,
            ["file_data"] = FileDataUrl(file)
        });

    public static JsonNode Compatible(ModelMessage message) =>
        Build(message, "text", static image => new JsonObject
        {
            ["type"] = "image_url",
            ["image_url"] = new JsonObject { ["url"] = DataUrl(image), ["detail"] = "auto" }
        }, static _ => throw new ArgumentException(
            "OpenAI-compatible chat completions does not define a portable PDF input shape."));

    public static JsonNode Anthropic(ModelMessage message) =>
        Build(message, "text", static image => new JsonObject
        {
            ["type"] = "image",
            ["source"] = new JsonObject
            {
                ["type"] = "base64", ["media_type"] = image.MediaType, ["data"] = image.Base64Data
            }
        }, static file => new JsonObject
        {
            ["type"] = "document",
            ["source"] = new JsonObject
            {
                ["type"] = "base64", ["media_type"] = file.MediaType, ["data"] = file.Base64Data
            },
            ["title"] = file.FileName
        });

    public static void AppendGemini(JsonArray parts, ModelMessage message)
    {
        if (!message.Images.IsDefaultOrEmpty)
            foreach (var image in message.Images)
                parts.Add(new JsonObject
                {
                    ["inlineData"] = new JsonObject
                    {
                        ["mimeType"] = image.MediaType, ["data"] = image.Base64Data
                    }
                });
        if (!message.Files.IsDefaultOrEmpty)
            foreach (var file in message.Files)
            parts.Add(new JsonObject
            {
                ["inlineData"] = new JsonObject
                {
                    ["mimeType"] = file.MediaType, ["data"] = file.Base64Data
                }
            });
    }

    private static JsonNode Build(
        ModelMessage message,
        string textType,
        Func<ModelImage, JsonObject> imageBlock,
        Func<ModelFile, JsonObject> fileBlock)
    {
        if (message.Images.IsDefaultOrEmpty && message.Files.IsDefaultOrEmpty)
            return JsonValue.Create(message.Content)!;
        var blocks = new JsonArray();
        if (!string.IsNullOrEmpty(message.Content))
            blocks.Add(new JsonObject { ["type"] = textType, ["text"] = message.Content });
        if (!message.Images.IsDefaultOrEmpty)
            foreach (var image in message.Images) blocks.Add(imageBlock(image));
        if (!message.Files.IsDefaultOrEmpty)
            foreach (var file in message.Files) blocks.Add(fileBlock(file));
        return blocks;
    }

    private static string DataUrl(ModelImage image) => $"data:{image.MediaType};base64,{image.Base64Data}";
    private static string FileDataUrl(ModelFile file) => $"data:{file.MediaType};base64,{file.Base64Data}";
}
