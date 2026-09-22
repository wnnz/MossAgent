using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser.Tools;

public sealed class BrowserDownloadTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.download",
        "点击页面元素并将浏览器下载保存为任务产物。",
        """{"type":"object","required":["selector"],"properties":{"selector":{"type":"string"}}}""",
        ToolRiskLevel.ExternalSideEffect,
        ToolCapability.Network | ToolCapability.FileWrite | ToolCapability.BrowserWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var session = context.BrowserSession
            ?? throw new InvalidOperationException("当前任务没有绑定浏览器会话。");
        var directory = context.ArtifactDirectory
            ?? throw new InvalidOperationException("当前任务没有配置产物目录。");
        Directory.CreateDirectory(directory);
        var prefix = $"browser-download-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}";
        var temporary = Path.Combine(directory, $".{prefix}.{Guid.NewGuid():N}.tmp");
        BrowserDownloadInfo download;
        try
        {
            await using (var stream = new FileStream(
                temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                download = await session.DownloadAsync(
                    request.Arguments.GetRequiredString("selector"), stream, cancellationToken);
            }

            var path = CreateDestinationPath(directory, prefix, download.SuggestedFileName);
            File.Move(temporary, path);
            var file = new FileInfo(path);
            var hash = await ComputeSha256Async(path, cancellationToken);
            var artifact = new ToolArtifact(
                download.SuggestedFileName, path, GetMediaType(file.Extension), file.Length, hash);
            return new ToolResult(
                true, $"浏览器下载已保存：{file.Name}", Artifacts: [artifact]);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    private static string CreateDestinationPath(
        string directory,
        string prefix,
        string suggestedFileName)
    {
        var extension = Path.GetExtension(Path.GetFileName(suggestedFileName));
        if (extension.Length > 16 || extension.Any(static character => !char.IsLetterOrDigit(character) && character != '.'))
        {
            extension = ".bin";
        }

        return Path.Combine(directory, prefix + extension.ToLowerInvariant());
    }

    private static async Task<string> ComputeSha256Async(
        string path,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash);
    }

    private static string GetMediaType(string extension) => extension.ToLowerInvariant() switch
    {
        ".json" => "application/json",
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".txt" or ".md" or ".csv" => "text/plain",
        ".zip" => "application/zip",
        _ => "application/octet-stream"
    };
}
