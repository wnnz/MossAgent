using System.Text;
using System.Text.Json.Nodes;
using MossAgent.Application.Models;

namespace MossAgent.Providers;

internal static class ModelRequestJson
{
    public static string Serialize(JsonObject root, bool containsImages, bool containsFiles = false)
    {
        var json = root.ToJsonString();
        var maximum = (containsImages ? ModelImageRequestValidation.MaximumJsonByteCount : 0L)
            + (containsFiles ? ModelFileRequestValidation.MaximumJsonByteCount : 0L);
        if (maximum > 0 && Encoding.UTF8.GetByteCount(json) > maximum)
            throw new ArgumentException("The binary input request body exceeds the size limit.", nameof(root));
        return json;
    }
}
