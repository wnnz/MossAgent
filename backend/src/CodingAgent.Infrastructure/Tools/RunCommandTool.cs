using System.Diagnostics;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;

namespace CodingAgent.Infrastructure.Tools;

public class RunCommandTool : ITool
{
    private const int DefaultTimeoutMs = 120_000;
    private const int MaxTimeoutMs = 600_000;
    private const int MaxOutputChars = 32_000;

    public string Name => "run_command";
    public string Description => "在工作区内执行命令（限时限量，Windows 用 cmd /c，其他平台用 bash -c）。参数: {\"command\": \"命令\", \"cwd\": \"可选相对工作目录\", \"timeout_ms\": 可选超时毫秒}";
    public string ParametersSchemaJson => """{"type":"object","properties":{"command":{"type":"string"},"cwd":{"type":"string"},"timeout_ms":{"type":"integer"}},"required":["command"]}""";

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default)
    {
        var command = ToolArguments.GetString(argumentsJson, "command");
        if (command.Length == 0)
        {
            return ToolResult.Fail("command 不能为空");
        }

        var timeoutMs = Math.Clamp(ToolArguments.GetInt(argumentsJson, "timeout_ms", DefaultTimeoutMs), 1_000, MaxTimeoutMs);

        try
        {
            var guard = new WorkspaceGuard(context.WorkspacePath);
            var cwd = ToolArguments.GetString(argumentsJson, "cwd");
            var workDir = cwd.Length > 0 ? guard.ResolveInsideWorkspace(cwd) : guard.WorkspaceRoot;

            var isWindows = OperatingSystem.IsWindows();
            var psi = isWindows
                ? new ProcessStartInfo("cmd.exe", $"/c {command}")
                : new ProcessStartInfo("/bin/bash", $"-c {Quote(command)}");
            psi.WorkingDirectory = workDir;
            psi.UseShellExecute = false;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.CreateNoWindow = true;

            using var process = Process.Start(psi);
            if (process is null)
            {
                return ToolResult.Fail("无法启动进程");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderrTask = process.StandardError.ReadToEndAsync(timeoutCts.Token);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                process.Kill(entireProcessTree: true);
                return ToolResult.Fail($"命令超时（{timeoutMs}ms）已终止");
            }

            var stdout = await stdoutTask.WaitAsync(CancellationToken.None);
            var stderr = await stderrTask.WaitAsync(CancellationToken.None);
            var output = $"exit={process.ExitCode}\n--- stdout ---\n{stdout}\n--- stderr ---\n{stderr}".Trim();
            return ToolResult.Ok(ToolArguments.Truncate(output, MaxOutputChars));
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Fail("命令已取消");
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or UnauthorizedAccessException)
        {
            return ToolResult.Fail($"执行失败: {ex.Message}");
        }
    }

    private static string Quote(string command) => "'" + command.Replace("'", "'\\''") + "'";
}
