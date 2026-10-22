using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers.Tests;

internal static class ModelFileTestFixture
{
    public static AttachmentDocument Pdf(string marker = "fixture")
    {
        var bytes = Encoding.ASCII.GetBytes($"%PDF-1.7\n%{marker}\n1 0 obj\n<<>>\nendobj\n%%EOF\n");
        return AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes);
    }

    public static ModelFile File(string fileName = "report.pdf", AttachmentDocument? document = null) =>
        ModelFile.FromAttachment(fileName, document ?? Pdf());

    public static ModelRequest Request(ProviderProtocol protocol, string text = "read this") =>
        ProviderStreamingTestFixture.CreateRequest(protocol) with
        {
            Model = ProviderStreamingTestFixture.CreateRequest(protocol).Model with
            {
                SupportsImages = false,
                SupportsFiles = true
            },
            Messages = [new(ModelRole.User, text, Files: [File()])]
        };

    public static string Build(ModelRequest request) => ModelImageTestFixture.Build(request);

    public static JsonElement Content(JsonElement root, ProviderProtocol protocol) =>
        ModelImageTestFixture.Content(root, protocol);
}
