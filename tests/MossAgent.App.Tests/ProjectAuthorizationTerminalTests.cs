using System.Diagnostics;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProjectAuthorizationTerminalTests
{
    [Fact]
    public async Task ProductionPipelineRetainsTerminalAuthorizationUntilSavedScopeRevokesItsProcessTree()
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync();
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var task = new AgentTask(Guid.NewGuid(), project.Id, "terminal revocation", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Running, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        await fixture.Tasks.SaveTaskAsync(task, fixture.Token);
        var context = fixture.Panels.CreateContext(task, project);
        var parentFile = Path.Combine(fixture.DataRoot, "terminal-parent.pid");
        var childFile = Path.Combine(fixture.DataRoot, "terminal-child.pid");
        var script = Path.Combine(fixture.DataRoot, "terminal-child.ps1");
        var late = Path.Combine(fixture.DataRoot, "terminal-late.txt");
        static string Quote(string text) => "'" + text.Replace("'", "''", StringComparison.Ordinal) + "'";
        await File.WriteAllTextAsync(script,
            $"[IO.File]::WriteAllText({Quote(childFile)},[string]$PID); Start-Sleep -Seconds 90; [IO.File]::WriteAllText({Quote(late)},'late')", fixture.Token);
        var processes = new List<Process>();
        try
        {
            var start = await ExecuteAsync(fixture, context, "terminal.start", new { });
            Assert.True(start.IsSuccess, start.Summary);
            var write = await ExecuteAsync(fixture, context, "terminal.write", new
            {
                input = $"[IO.File]::WriteAllText({Quote(parentFile)},[string]$PID); Start-Process pwsh -WindowStyle Hidden -WorkingDirectory {Quote(project.PrimaryDirectory)} -ArgumentList '-NoProfile','-File',{Quote(script)}\r"
            });
            Assert.True(write.IsSuccess, write.Summary);
            processes.Add(await BindAsync(parentFile, fixture.Token));
            processes.Add(await BindAsync(childFile, fixture.Token));
            Assert.All(processes, process => Assert.False(process.HasExited));
            await fixture.Tasks.SaveProjectAsync(project with { AuthorizedDirectories = [fixture.DataRoot] }, fixture.Token);
            foreach (var process in processes)
                await process.WaitForExitAsync(fixture.Token).WaitAsync(TimeSpan.FromSeconds(10), fixture.Token);
            Assert.False(File.Exists(late));
            var refused = await ExecuteAsync(fixture, context, "terminal.start", new { });
            Assert.Equal("workspace_authorization_changed", refused.ErrorCode);
            var audits = await fixture.ToolAudits.GetRecentAsync(task.Id, 10, fixture.Token);
            Assert.Equal(3, audits.Count);
            Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
            Assert.Single(audits, audit => audit.ErrorCode == "workspace_authorization_changed");
            Assert.Empty(fixture.Server.Requests);
        }
        finally
        {
            foreach (var process in processes)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                try { await process.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken); }
                finally { process.Dispose(); }
            }
        }
    }

    private static Task<ToolResult> ExecuteAsync(WorkspaceModelSelectionFixture fixture, ToolExecutionContext context,
        string name, object arguments) => fixture.Tools.ExecuteAsync(
            new ToolRequest(Guid.NewGuid().ToString("N"), name, JsonSerializer.SerializeToElement(arguments)), context, fixture.Token);

    private static async Task<Process> BindAsync(string path, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        while (true)
        {
            if (File.Exists(path) && int.TryParse(await File.ReadAllTextAsync(path, timeout.Token), out var pid))
            {
                var process = Process.GetProcessById(pid);
                _ = process.Handle;
                return process;
            }
            await Task.Delay(20, timeout.Token);
        }
    }
}
