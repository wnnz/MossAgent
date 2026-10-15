using System.Text.Json;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceMcpFlowTests
{
    [Fact]
    public async Task SettingsToApprovedMcpCall_RunsRealServerAndRestoresConversationAndAudit()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("实际 MCP 回复 🚀",
            reviewRoot: ReviewRoot("approved"));
        using var startup = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(fixture.ServerRoot, "startup.json"), fixture.Token));
        Assert.Equal("界面参数 with spaces", startup.RootElement.GetProperty("argument").GetString());
        Assert.Equal(fixture.ServerRoot, startup.RootElement.GetProperty("directory").GetString());
        Assert.Equal("界面环境 🚀", startup.RootElement.GetProperty("environment").GetString());
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        Assert.Equal(fixture.ToolName, fixture.Approval.ToolName);
        Assert.False(File.Exists(Path.Combine(fixture.ServerRoot, "called.json")));
        var preview = Assert.IsType<ToolApprovalContentViewModel>(fixture.Approval.ContentPreview);
        Assert.False(preview.IsAvailable);
        Assert.False(preview.ToggleCommand.CanExecute(null));
        Assert.Equal(string.Empty, preview.PageText);

        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.Equal("实际 MCP 回复 🚀", (await fixture.AssertPersistedAsync(null, true)).Content);
        Assert.True(File.Exists(Path.Combine(fixture.ServerRoot, "called.json")));
        Assert.False(preview.IsOpen);
        Assert.Equal(2, fixture.Model.Requests.Count);
        Assert.Contains(fixture.Workspace.Messages, message => message.Content == "MCP 链路验收已结束。");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingCall_RejectionOrStopNeverInvokesMcpServer(bool cancel)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run");
        fixture.Start();
        await fixture.WaitForApprovalAsync();

        if (cancel) fixture.Workspace.PrimaryActionCommand.Execute(null);
        else fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.False(File.Exists(Path.Combine(fixture.ServerRoot, "called.json")));
        await fixture.AssertPersistedAsync(cancel ? "tool_cancelled" : "approval_denied",
            cancel ? null : false, cancel ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed);
        Assert.Equal(cancel ? 1 : 2, fixture.Model.Requests.Count);
    }

    [Fact]
    public async Task UnknownSideEffectMcpTool_ReadOnlyPolicyRejectsBeforeApproval()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("must not run");
        var prompts = 0;
        fixture.Approval.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ApprovalViewModel.IsPending) && fixture.Approval.IsPending) prompts++;
        };

        fixture.Start(ApprovalPolicy.ReadOnly);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(0, prompts);
        Assert.False(File.Exists(Path.Combine(fixture.ServerRoot, "called.json")));
        await fixture.AssertPersistedAsync("capability_denied", false);
    }

    [Fact]
    public async Task ApprovedRunningCall_StopNotifiesServerAndPersistsCancellation()
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync("wait");
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForFileAsync("waiting.id");

        fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitForCompletionAsync();

        await fixture.WaitForFileAsync("cancelled.json");
        await fixture.AssertPersistedAsync("tool_cancelled", true, AgentTaskStatus.Cancelled);
        Assert.Single(fixture.Model.Requests);
    }

    [Theory]
    [InlineData("verbose", null)]
    [InlineData("error", "tool_failed")]
    public async Task VerboseOrFailingMcpCall_ProducesBoundedSafePersistedResult(string message, string? error)
    {
        await using var fixture = await WorkspaceMcpFixture.CreateAsync(message);
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        var result = await fixture.AssertPersistedAsync(error, true);

        Assert.InRange(result.OutputCharacterCount, 1, 64 * 1024);
        Assert.DoesNotContain("fixture-secret-marker", result.Summary + result.Content);
        Assert.DoesNotContain("fixture-secret-marker", fixture.Model.Requests[1]);
        if (error is not null) return;
        Assert.Contains("[截断]", result.Content);
        using var request = JsonDocument.Parse(fixture.Model.Requests[1]);
        var output = request.RootElement.GetProperty("input").EnumerateArray().Single(item =>
            item.TryGetProperty("type", out var type) && type.GetString() == "function_call_output")
            .GetProperty("output").GetString()!;
        Assert.InRange(output.Length, 1, 70_000);
    }

    private static string? ReviewRoot(string scenario)
    {
        var root = Environment.GetEnvironmentVariable("MOSSAGENT_MCP_QA_ROOT");
        return string.IsNullOrWhiteSpace(root) ? null : Path.Combine(root, scenario);
    }
}
