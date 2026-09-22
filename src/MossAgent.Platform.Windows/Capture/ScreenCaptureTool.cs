using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Platform.Windows.Capture;

public sealed partial class ScreenCaptureTool : IAgentTool
{
    private const int ScreenWidth = 0;
    private const int ScreenHeight = 1;

    public ToolDescriptor Descriptor { get; } = new(
        "screen.capture",
        "截取主显示器或指定屏幕矩形区域并保存为任务产物。",
        """{"type":"object","properties":{"x":{"type":"integer"},"y":{"type":"integer"},"width":{"type":"integer","minimum":1},"height":{"type":"integer","minimum":1}}}""",
        ToolRiskLevel.HighRisk,
        ToolCapability.ScreenCapture);

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var directory = context.ArtifactDirectory
            ?? throw new InvalidOperationException("当前任务没有配置产物目录。");
        var x = request.Arguments.GetOptionalInt32("x") ?? 0;
        var y = request.Arguments.GetOptionalInt32("y") ?? 0;
        var width = request.Arguments.GetOptionalInt32("width") ?? GetSystemMetrics(ScreenWidth);
        var height = request.Arguments.GetOptionalInt32("height") ?? GetSystemMetrics(ScreenHeight);
        if (width is < 1 or > 16384 || height is < 1 or > 16384)
        {
            return ToolResult.Failure("截图尺寸无效。", "invalid_capture_bounds");
        }

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"screen-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmssfff}.png");
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(x, y, 0, 0, new Size(width, height), CopyPixelOperation.SourceCopy);
        bitmap.Save(path, ImageFormat.Png);
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        var artifact = new ToolArtifact(
            Path.GetFileName(path), path, "image/png", bytes.LongLength,
            Convert.ToHexString(SHA256.HashData(bytes)));
        return new ToolResult(true, "屏幕截图已保存。", Artifacts: [artifact]);
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);
}

