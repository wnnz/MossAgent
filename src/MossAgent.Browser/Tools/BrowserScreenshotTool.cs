using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Browser.Tools;

public sealed class BrowserScreenshotTool : IAgentTool
{
    public ToolDescriptor Descriptor { get; } = new(
        "browser.screenshot",
        "截取当前浏览器页面并保存为任务产物。",
        """{"type":"object","properties":{"fullPage":{"type":"boolean"}}}""",
        ToolRiskLevel.ReadOnly,
        ToolCapability.BrowserRead | ToolCapability.ScreenCapture);

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
        var bytes = await session.ScreenshotAsync(
            request.Arguments.GetOptionalBoolean("fullPage") ?? false,
            cancellationToken);
        var path = Path.Combine(directory, $"browser-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.png");
        await File.WriteAllBytesAsync(path, bytes, cancellationToken);
        var artifact = new ToolArtifact(
            Path.GetFileName(path), path, "image/png", bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)));
        return new ToolResult(true, "浏览器截图已保存。", Artifacts: [artifact]);
    }
}
