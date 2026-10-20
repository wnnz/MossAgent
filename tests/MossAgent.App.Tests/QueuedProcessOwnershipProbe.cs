using System.Diagnostics;
using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class QueuedProcessOwnershipProbe(string root) : IDisposable
{
    private readonly List<Process> _processes = [];
    private readonly DateTime _created = DateTime.UtcNow;

    public async Task CreateAsync(string name, CancellationToken token)
    {
        var location = root.Replace("'", "''", StringComparison.Ordinal);
        var setLocation = $"Set-Location -LiteralPath '{location}'; [Environment]::CurrentDirectory = (Get-Location).Path; ";
        var wait = $"$ownedDeadline = [DateTime]::UtcNow.AddSeconds(20); "
            + $"while (!(Test-Path -LiteralPath '{name}.release') -and [DateTime]::UtcNow -lt $ownedDeadline) {{ Start-Sleep -Milliseconds 20 }}; "
            + $"if (!(Test-Path -LiteralPath '{name}.release')) {{ exit 7 }}; ";
        var child = setLocation + Publish(name + "-child")
            + wait
            + $"[IO.File]::WriteAllText('{name}-child.done', 'done')";
        var parent = setLocation + Publish(name + "-parent")
            + $"$ownedChild = Start-Process pwsh -WindowStyle Hidden -PassThru -WorkingDirectory (Get-Location).Path "
            + $"-ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-File','{name}-child.ps1'; "
            + wait
            + $"$ownedChild.WaitForExit(); [Console]::Out.Write('{name}-completed')";
        await File.WriteAllTextAsync(Path.Combine(root, name + "-child.ps1"), child, token);
        await File.WriteAllTextAsync(Path.Combine(root, name + "-parent.ps1"), parent, token);
    }

    private static string Publish(string name) =>
        $"[IO.File]::WriteAllText('{name}.pid.tmp', [string]$PID); "
        + $"[IO.File]::Move('{name}.pid.tmp', '{name}.pid', $true); "
        + $"[IO.File]::WriteAllText('{name}.ready', 'ready'); ";

    public async Task<Process> BindAsync(string name, CancellationToken token)
    {
        while (!File.Exists(Path.Combine(root, name + ".ready"))) await Task.Delay(15, token);
        var id = int.Parse(await File.ReadAllTextAsync(Path.Combine(root, name + ".pid"), token));
        var process = Process.GetProcessById(id);
        try
        {
            _ = process.Handle;
            if (process.StartTime.ToUniversalTime() < _created.AddSeconds(-1)
                || !string.Equals(Path.GetFileName(process.MainModule?.FileName), "pwsh.exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The generated process marker has an unexpected owner.");
            _processes.Add(process);
            return process;
        }
        catch { process.Dispose(); throw; }
    }

    public Task ReleaseAsync(string name, CancellationToken token) =>
        File.WriteAllTextAsync(Path.Combine(root, name + ".release"), "release", token);

    public static string? Response(JsonElement request, int _)
    {
        var input = request.GetProperty("input").EnumerateArray().ToArray();
        if (input.Any(message => message.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")) return null;
        var last = input.LastOrDefault(message => message.TryGetProperty("role", out var role) && role.GetString() == "user");
        var prompt = last.GetProperty("content").GetString();
        if (prompt is not ("first-owned-process" or "second-owned-process")) return null;
        var name = prompt.StartsWith("first", StringComparison.Ordinal) ? "first" : "second";
        var tool = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("shell_run__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new { type = "response.output_item.done", item = new
        { type = "function_call", call_id = name + "-process", name = tool,
            arguments = JsonSerializer.Serialize(new { command = $"& './{name}-parent.ps1'" }) } });
    }

    public void Dispose()
    {
        foreach (var process in _processes) process.Dispose();
    }
}
