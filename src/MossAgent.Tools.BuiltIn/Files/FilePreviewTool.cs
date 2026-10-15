using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FilePreviewTool(AuthorizedPathResolver paths) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.preview_file", FileToolText.Get("PreviewDescription"),
        """{"type":"object","required":["path"],"properties":{"path":{"type":"string","minLength":1}} ,"additionalProperties":false}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        if (request.Arguments.EnumerateObject().Count() != 1) throw new ArgumentException();
        paths.Resolve(request.Arguments.GetRequiredString("path"), context);
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        await using var source = FileSnapshotReader.Open(path);
        if (source.Length > FilePreviewFormat.MaximumBytes)
            return ToolResult.Failure(FileToolText.Get("PreviewTooLarge"), "preview_too_large");
        var bytes = new byte[checked((int)source.Length)];
        await source.ReadExactlyAsync(bytes, cancellationToken);
        string mediaType;
        try { mediaType = FilePreviewFormat.Detect(bytes); }
        catch (Exception exception) when (exception is InvalidDataException or System.Text.DecoderFallbackException)
        { return ToolResult.Failure(FileToolText.Get("PreviewUnsupported"), "preview_unsupported"); }
        paths.Resolve(path, context);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = context.ArtifactDirectory ?? throw new InvalidOperationException();
        RejectArtifactLinks(directory);
        Directory.CreateDirectory(directory);
        RejectArtifactLinks(directory);
        var destination = Path.Combine(directory, $"preview-{Guid.NewGuid():N}.snapshot");
        try
        {
            await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous))
                await output.WriteAsync(bytes, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            var hash = Convert.ToHexString(SHA256.HashData(bytes));
            context.FileSnapshots.Record(context.TaskId, FileSnapshotReader.CacheKey(path), new FileSnapshot(hash));
            return new ToolResult(true, FileToolText.Get("PreviewSummary"), Artifacts:
                [new ToolArtifact(Path.GetFileName(path), destination, mediaType, bytes.Length, hash)]);
        }
        catch
        {
            File.Delete(destination);
            throw;
        }
    }

    private static void RejectArtifactLinks(string directory)
    {
        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
            if (current.LinkTarget is not null) throw new UnauthorizedAccessException();
    }
}
