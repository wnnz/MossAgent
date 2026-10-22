using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Providers.Tests;

public sealed class ModelFileRequestTests
{
    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public void NativeDocumentBlocksPreserveTextAndPdfPayload(ProviderProtocol protocol)
    {
        var request = ModelFileTestFixture.Request(protocol);
        var file = request.Messages[0].Files[0];

        using var document = JsonDocument.Parse(ModelFileTestFixture.Build(request));
        var content = ModelFileTestFixture.Content(document.RootElement, protocol);

        Assert.Equal(2, content.GetArrayLength());
        Assert.Equal("read this", content[0].GetProperty("text").GetString());
        if (protocol == ProviderProtocol.OpenAiResponses)
        {
            var block = content[1];
            Assert.Equal("input_file", block.GetProperty("type").GetString());
            Assert.Equal(file.FileName, block.GetProperty("filename").GetString());
            Assert.Equal($"data:{file.MediaType};base64,{file.Base64Data}",
                block.GetProperty("file_data").GetString());
        }
        else if (protocol == ProviderProtocol.AnthropicMessages)
        {
            var block = content[1];
            Assert.Equal("document", block.GetProperty("type").GetString());
            Assert.Equal(file.FileName, block.GetProperty("title").GetString());
            var source = block.GetProperty("source");
            Assert.Equal("base64", source.GetProperty("type").GetString());
            Assert.Equal(file.MediaType, source.GetProperty("media_type").GetString());
            Assert.Equal(file.Base64Data, source.GetProperty("data").GetString());
        }
        else
        {
            var inline = content[1].GetProperty("inlineData");
            Assert.Equal(file.MediaType, inline.GetProperty("mimeType").GetString());
            Assert.Equal(file.Base64Data, inline.GetProperty("data").GetString());
        }
    }

    [Fact]
    public void OpenAiCompatibleRejectsPdfInsteadOfGuessingAChatCompletionsShape()
    {
        var request = ModelFileTestFixture.Request(ProviderProtocol.OpenAiCompatible);

        var error = Assert.Throws<ArgumentException>(() => ModelFileTestFixture.Build(request));

        Assert.Contains("portable PDF", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Messages[0].Files[0].Base64Data, error.Message);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public void UnsupportedModelsAreRejectedBeforeAnyRequestIsSent(ProviderProtocol protocol)
    {
        var request = ModelFileTestFixture.Request(protocol);
        request = request with { Model = request.Model with { SupportsFiles = false } };

        var error = Assert.Throws<ArgumentException>(() => ModelFileTestFixture.Build(request));

        Assert.Contains("does not support file", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(request.Provider.ApiKey, error.Message);
        Assert.DoesNotContain(request.Messages[0].Files[0].Base64Data, error.Message);
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public void FilesCannotBeAttachedToOtherRolesOrToolMetadata(ProviderProtocol protocol)
    {
        var request = ModelFileTestFixture.Request(protocol);
        var user = request.Messages[0];
        foreach (var role in new[] { ModelRole.System, ModelRole.Assistant, ModelRole.Tool, ModelRole.ToolObservation })
            Assert.Throws<ArgumentException>(() =>
                ModelFileTestFixture.Build(request with { Messages = [user with { Role = role }] }));
        Assert.Throws<ArgumentException>(() =>
            ModelFileTestFixture.Build(request with { Messages = [user with { ToolCallId = "call" }] }));
        Assert.Throws<ArgumentException>(() =>
            ModelFileTestFixture.Build(request with { Messages = [user with { ToolName = "tool" }] }));
    }

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses)]
    [InlineData(ProviderProtocol.AnthropicMessages)]
    [InlineData(ProviderProtocol.GeminiGenerateContent)]
    public void FileCountAndAggregateBytesAreBoundedAcrossMessages(ProviderProtocol protocol)
    {
        var request = ModelFileTestFixture.Request(protocol);
        var file = request.Messages[0].Files[0];
        var four = new ModelMessage(ModelRole.User, "", Files: [file, file, file, file]);
        ModelFileTestFixture.Build(request with { Messages = [four] });
        Assert.Throws<ArgumentException>(() => ModelFileTestFixture.Build(request with
        {
            Messages = [four, new(ModelRole.User, "", Files: [file])]
        }));

        var bytes = new byte[9 * 1024 * 1024];
        "%PDF-"u8.CopyTo(bytes);
        "%%EOF"u8.CopyTo(bytes.AsSpan(bytes.Length - 5));
        var large = ModelFile.FromAttachment("large.pdf",
            AttachmentDocument.FromDecodedBytes(AttachmentDocument.PdfMediaType, bytes));
        var tooLarge = new ModelMessage(ModelRole.User, "", Files: [large, large, large, large]);
        Assert.Throws<ArgumentException>(() =>
            ModelFileTestFixture.Build(request with { Messages = [tooLarge] }));
    }

    [Fact]
    public void DiagnosticSerializationDoesNotExposeFileBytes()
    {
        var message = new ModelMessage(ModelRole.User, "read", Files: [ModelFileTestFixture.File()]);

        var json = JsonSerializer.Serialize(message);

        Assert.DoesNotContain(ModelFileTestFixture.File().Base64Data, json);
        Assert.DoesNotContain("Files", json);
    }
}
