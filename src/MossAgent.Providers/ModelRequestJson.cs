using System.Text;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;

namespace MossAgent.Providers;

internal static class ModelRequestJson
{
    public static string Serialize(JsonObject root, bool containsImages)
    {
        var json = root.ToJsonString();
        if (containsImages
            && Encoding.UTF8.GetByteCount(json) > ModelImageRequestValidation.MaximumJsonByteCount)
            throw new ArgumentException("The image request body exceeds the size limit.", nameof(root));
        return json;
    }
}
