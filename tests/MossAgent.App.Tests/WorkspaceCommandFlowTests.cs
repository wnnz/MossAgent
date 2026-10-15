using System.Diagnostics;
using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceCommandFlowTests
{
    private const string SuccessfulCommand =
        "[IO.File]::WriteAllText('completed.txt', 'approved'); [Console]::Out.Write('shell-ok')";

    [Fact]
    public async Task SettingsToApprovedCommand_RunsRealProcessAndRestoresConversationAndAudit()
    {
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(SuccessfulCommand,
            reviewRoot: ReviewRoot("approved"));
        fixture.Start();
        await fixture.WaitForApprovalAsync();

        Assert.Equal("shell.run", fixture.Approval.ToolName);
        Assert.Equal(fixture.ProjectRoot, fixture.Approval.WorkingDirectory);
        Assert.False(File.Exists(fixture.MarkerPath));
        var pending = await fixture.WaitForToolPhaseAsync(ToolExecutionPhase.AwaitingApproval);
        var phases = new List<ToolExecutionPhase>();
        pending.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ChatMessageViewModel.ToolPhase) && pending.ToolPhase is { } phase) phases.Add(phase);
        };
        Assert.False(pending.HasToolDetails);
        var preview = Assert.IsType<ToolApprovalContentViewModel>(fixture.Approval.ContentPreview);
        Assert.False(preview.IsOpen);
        preview.ToggleCommand.Execute(null);
        Assert.Contains(SuccessfulCommand, preview.PageText);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.Equal([ToolExecutionPhase.Executing, ToolExecutionPhase.Finalizing], phases);
        Assert.Same(pending, Assert.Single(fixture.Workspace.Messages, message => message.IsTool));
        Assert.Null(pending.ToolPhase);
        Assert.Equal("approved", await File.ReadAllTextAsync(fixture.MarkerPath, fixture.Token));
        var result = await fixture.AssertPersistedAsync(null, true);
        Assert.Equal("shell-ok", result.Content);
        Assert.False(preview.IsOpen);
        Assert.Equal(string.Empty, preview.PageText);
        Assert.Equal(2, fixture.Server.Requests.Count);
        Assert.Contains(fixture.Workspace.Messages, message => message.Content == "本机命令验收已结束。");
        Assert.Contains("shell-ok", fixture.Server.Requests[1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingCommand_RejectionOrStopDoesNotLaunchProcess(bool cancel)
    {
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(SuccessfulCommand);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        var pending = Assert.Single(fixture.Workspace.Messages, message => message.IsTool && message.IsToolRunning);
        Assert.Equal("shell.run", pending.Tool!.ToolName);
        Assert.False(pending.HasToolDetails);
        if (cancel) fixture.Workspace.PrimaryActionCommand.Execute(null);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.False(File.Exists(fixture.MarkerPath));
        await fixture.AssertPersistedAsync(cancel ? "tool_cancelled" : "approval_denied",
            cancel ? null : false, cancel ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed);
        Assert.Equal(cancel ? 1 : 2, fixture.Server.Requests.Count);
    }

    [Theory]
    [InlineData(false, "capability_denied")]
    [InlineData(true, "path_denied")]
    public async Task ReadOnlyOrOutsideWorkingDirectory_IsDeniedBeforeApproval(bool escape, string error)
    {
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(SuccessfulCommand, escape);
        var approvals = 0;
        fixture.Approval.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ApprovalViewModel.IsPending) && fixture.Approval.IsPending) approvals++;
        };
        fixture.Start(escape ? ApprovalPolicy.AskEveryTime : ApprovalPolicy.ReadOnly);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(0, approvals);
        Assert.False(File.Exists(fixture.MarkerPath));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "outside", "completed.txt")));
        await fixture.AssertPersistedAsync(error, false);
    }

    [Fact]
    public async Task ApprovedLongCommand_StopTerminatesRealParentAndChildBeforeCompletion()
    {
        var command = "$child = Start-Process pwsh -WindowStyle Hidden -PassThru "
            + "-ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 30'; "
            + WorkspaceCommandFixture.PublishPid("child.pid", "$child.Id") + "; "
            + WorkspaceCommandFixture.PublishPid("shell.pid", "$PID") + "; "
            + "Start-Sleep -Seconds 30; [IO.File]::WriteAllText('completed.txt', 'late')";
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(command);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        var pending = await fixture.WaitForToolPhaseAsync(ToolExecutionPhase.AwaitingApproval);
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForFileAsync("shell.pid");
        Assert.Same(pending, await fixture.WaitForToolPhaseAsync(ToolExecutionPhase.Executing));
        using var parent = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.ProjectRoot, "shell.pid"), fixture.Token)));
        using var child = Process.GetProcessById(int.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.ProjectRoot, "child.pid"), fixture.Token)));
        Assert.False(parent.HasExited);
        Assert.False(child.HasExited);

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitForCompletionAsync();

        Assert.Same(pending, Assert.Single(fixture.Workspace.Messages, message => message.IsTool));
        Assert.Null(pending.ToolPhase);
        Assert.True(parent.HasExited);
        Assert.True(child.HasExited);
        Assert.False(File.Exists(fixture.MarkerPath));
        await fixture.AssertPersistedAsync("tool_cancelled", true, AgentTaskStatus.Cancelled);
        Assert.Single(fixture.Server.Requests);
    }

    [Theory]
    [InlineData("[Console]::Error.Write('stderr-marker'); exit 7", "process_failed")]
    [InlineData("[Console]::Out.Write(('x' * 200000))", null)]
    public async Task FailedOrVerboseCommand_ProducesBoundedPersistedToolResult(string command, string? error)
    {
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(command,
            reviewRoot: error is null ? null : ReviewRoot("failed"));
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        var result = await fixture.AssertPersistedAsync(error, true);
        Assert.InRange(result.OutputCharacterCount, 1, 64 * 1024);
        if (error is null)
        {
            Assert.Contains("[截断]", result.Content);
            using var request = JsonDocument.Parse(fixture.Server.Requests[1]);
            var output = request.RootElement.GetProperty("input").EnumerateArray().Single(item =>
                item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
                .GetProperty("output").GetString()!;
            Assert.True(output.Length < 70_000);
        }
        else
        {
            Assert.Contains("stderr-marker", result.Content);
            Assert.DoesNotContain("stderr-marker", result.Summary);
            Assert.True(Assert.Single(fixture.Workspace.Messages,
                message => message.ToolCallId == "command-flow").Tool!.HasDetails);
        }
    }

    [Fact]
    public async Task ApprovedParentExit_ReclaimsBackgroundChildBeforeConversationCompletes()
    {
        var command = "$child = Start-Process pwsh -WindowStyle Hidden -PassThru "
            + "-ArgumentList '-NoProfile','-Command','Start-Sleep -Seconds 30'; "
            + WorkspaceCommandFixture.PublishPid("child.pid", "$child.Id") + "; "
            + "while (!(Test-Path 'release-parent')) { Start-Sleep -Milliseconds 20 }; "
            + "[Console]::Out.Write('shell-ok')";
        await using var fixture = await WorkspaceCommandFixture.CreateAsync(command);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForFileAsync("child.pid");
        var pid = int.Parse(await File.ReadAllTextAsync(Path.Combine(fixture.ProjectRoot, "child.pid"), fixture.Token));
        using var child = Process.GetProcessById(pid);
        _ = child.Handle;
        Assert.False(child.HasExited);

        await File.WriteAllTextAsync(Path.Combine(fixture.ProjectRoot, "release-parent"), "go", fixture.Token);
        await fixture.WaitForCompletionAsync();

        Assert.True(child.HasExited);
        Assert.Equal("shell-ok", (await fixture.AssertPersistedAsync(null, true)).Content);
        Assert.Equal(2, fixture.Server.Requests.Count);
    }

    private static string? ReviewRoot(string scenario)
    {
        var root = Environment.GetEnvironmentVariable("MOSSAGENT_COMMAND_QA_ROOT");
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, scenario);
    }
}
