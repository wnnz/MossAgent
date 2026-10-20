using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal static class WorkspaceFollowupAuditAssertions
{
    public static async Task<ToolExecutionAuditRecord> ReadWriteAsync(
        WorkspaceModelSelectionFixture fixture, Guid taskId, bool successfulWrite)
    {
        var statistics = fixture.Workspace.FileChanges.Statistics;
        if (successfulWrite)
        {
            await fixture.WaitUntilAsync(() => statistics.HasTotals || statistics.HasError);
            await statistics.StopAsync();
        }
        var audits = await fixture.ToolAudits.GetRecentAsync(taskId, 10, fixture.Token);
        var write = Validate(audits, taskId, successfulWrite);
        if (successfulWrite)
        {
            Assert.True(statistics.HasTotals);
            Assert.False(statistics.HasError);
            Assert.Equal("+1", statistics.AddedLabel);
            Assert.Equal("-0", statistics.RemovedLabel);
        }
        return write;
    }

    public static ToolExecutionAuditRecord Validate(
        IReadOnlyList<ToolExecutionAuditRecord> audits, Guid taskId, bool successfulWrite)
    {
        Assert.InRange(audits.Count, successfulWrite ? 2 : 1, successfulWrite ? 9 : 1);
        Assert.Equal(audits.Count, audits.Select(item => item.Id).Distinct().Count());
        Assert.Equal(audits.Count, audits.Select(item => item.CallId).Distinct().Count());
        Assert.All(audits, item => { Assert.Equal(taskId, item.TaskId); Assert.NotNull(item.CompletedAt); Assert.Null(item.InterruptedAt); });
        var write = Assert.Single(audits, item => item.ToolName == "filesystem.write_file");
        var statistics = audits.Where(item => item.Id != write.Id).OrderBy(item => item.StartedAt).ToArray();
        if (!successfulWrite) { Assert.Empty(statistics); return write; }
        Assert.NotEmpty(statistics);
        foreach (var item in statistics)
        {
            Assert.Equal("filesystem.change_statistics", item.ToolName);
            Assert.StartsWith("statistics-", item.CallId, StringComparison.Ordinal);
            Assert.Equal(ToolRiskLevel.ReadOnly, item.RiskLevel);
            Assert.Equal(ApprovalPolicy.AskEveryTime, item.ApprovalPolicy);
            Assert.True(item.ApprovalGranted);
            Assert.NotNull(item.IsSuccess);
            if (item.IsSuccess == true) Assert.Null(item.ErrorCode);
            else Assert.Equal("tool_cancelled", item.ErrorCode);
        }
        var current = statistics[^1];
        Assert.True(current.IsSuccess, JsonSerializer.Serialize(audits));
        Assert.All(statistics[..^1], item => Assert.True(item.CompletedAt <= current.StartedAt));
        return write;
    }
}
