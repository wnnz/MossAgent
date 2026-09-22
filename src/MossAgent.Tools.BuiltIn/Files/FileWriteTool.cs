using System.Security.Cryptography;
using System.Text;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Tools.BuiltIn.Files;

public sealed class FileWriteTool(AuthorizedPathResolver paths) : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "filesystem.write_file",
        "原子写入授权目录内的文本文件，可校验原文件哈希。",
        """{"type":"object","required":["path","content"],"properties":{"path":{"type":"string"},"content":{"type":"string"},"expectedSha256":{"type":["string","null"]}}}""",
        ToolRiskLevel.Mutation,
        ToolCapability.FileWrite);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var path = paths.Resolve(request.Arguments.GetRequiredString("path"), context);
        var content = request.Arguments.GetRequiredString("content");
        var expectedHash = request.Arguments.GetOptionalString("expectedSha256");
        await VerifyExpectedHashAsync(path, expectedHash, cancellationToken);

        var directory = Path.GetDirectoryName(path)
            ?? throw new InvalidOperationException("无法确定目标目录。");
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);
            File.Move(temporary, path, true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }

        return ToolResult.Success($"已写入 {content.Length} 个字符：{path}");
    }

    private static async Task VerifyExpectedHashAsync(
        string path,
        string? expectedSha256,
        CancellationToken cancellationToken)
    {
        if (expectedSha256 is null || !File.Exists(path))
        {
            return;
        }

        await using var stream = File.OpenRead(path);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
        if (!hash.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("文件已被其他进程修改，已拒绝覆盖。");
        }
    }
}
