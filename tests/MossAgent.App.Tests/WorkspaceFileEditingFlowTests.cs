using System.Security.Cryptography;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceFileEditingFlowTests
{
    [Theory]
    [InlineData(false, "beta\n")]
    [InlineData(false, "")]
    [InlineData(true, "beta\n")]
    public async Task SettingsToApprovedEdit_UpdatesFileAndPersistsConversationAndAudit(bool replace, string content)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, content);
        var expectedHash = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixture.FilePath, fixture.Token)));
        fixture.Start();
        await fixture.WaitForApprovalAsync();

        Assert.Equal(replace ? "filesystem.replace_text" : "filesystem.write_file", fixture.Approval.ToolName);
        Assert.Equal(fixture.ProjectRoot, fixture.Approval.WorkingDirectory);
        Assert.Contains(fixture.TaskId.ToString(), fixture.Approval.TaskLabel, StringComparison.Ordinal);
        var preview = Assert.IsType<ToolApprovalContentViewModel>(fixture.Approval.ContentPreview);
        Assert.False(preview.IsOpen);
        preview.ToggleCommand.Execute(null);
        Assert.True(preview.IsOpen);
        Assert.Equal(expectedHash, fixture.Server.ExpectedSha256, ignoreCase: true);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(content, await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.False(preview.IsOpen);
        Assert.Equal(string.Empty, preview.PageText);
        await fixture.AssertPersistedAsync(null, true, AgentTaskStatus.Completed);
        Assert.Equal(3, fixture.Server.Requests.Count);
        Assert.Contains(fixture.Workspace.Messages, message => message.Content == "本机文件流程已结束。");
    }

    [Theory]
    [InlineData(false, "deny", "approval_denied")]
    [InlineData(true, "deny", "approval_denied")]
    [InlineData(false, "conflict", "file_changed")]
    [InlineData(true, "conflict", "file_changed")]
    [InlineData(false, "cancel", "tool_cancelled")]
    [InlineData(true, "cancel", "tool_cancelled")]
    public async Task PendingEdit_DenialConflictAndCancellationPreserveFiles(bool replace, string action, string error)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n");
        fixture.Start();
        await fixture.WaitForApprovalAsync();
        var preview = Assert.IsType<ToolApprovalContentViewModel>(fixture.Approval.ContentPreview);
        preview.ToggleCommand.Execute(null);
        if (action == "conflict")
        {
            await File.WriteAllTextAsync(fixture.FilePath, "external change", fixture.Token);
            fixture.Approval.ApproveCommand.Execute(fixture.Approval.CurrentRequestId);
        }
        else if (action == "deny") fixture.Approval.DenyCommand.Execute(fixture.Approval.CurrentRequestId);
        else fixture.Workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(action == "conflict" ? "external change" : "alpha\n",
            await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal(string.Empty, preview.PageText);
        await fixture.AssertPersistedAsync(error, action == "cancel" ? null : action == "conflict", action == "cancel"
            ? AgentTaskStatus.Cancelled : AgentTaskStatus.Completed);
        Assert.Equal(action == "cancel" ? 2 : 3, fixture.Server.Requests.Count);
    }

    [Theory]
    [InlineData(false, false, "capability_denied")]
    [InlineData(true, false, "capability_denied")]
    [InlineData(false, true, "path_denied")]
    [InlineData(true, true, "path_denied")]
    public async Task ReadOnlyOrEscapingMutation_IsRejectedWithoutAnApprovalPrompt(bool replace, bool escape, string error)
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(replace, "beta\n", escape);
        var prompts = 0;
        fixture.Approval.PropertyChanged += (_, change) =>
        {
            if (change.PropertyName == nameof(ApprovalViewModel.IsPending) && fixture.Approval.IsPending) prompts++;
        };
        fixture.Start(escape ? ApprovalPolicy.AskEveryTime : ApprovalPolicy.ReadOnly);
        await fixture.WaitForCompletionAsync();

        Assert.Equal(0, prompts);
        Assert.Equal("alpha\n", await File.ReadAllTextAsync(fixture.FilePath, fixture.Token));
        Assert.Equal("outside sentinel", await File.ReadAllTextAsync(fixture.OutsidePath, fixture.Token));
        await fixture.AssertPersistedAsync(error, false, AgentTaskStatus.Completed);
    }
}
