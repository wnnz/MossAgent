using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class InterruptedAuditPresentationTests
{
    [Fact]
    public void InterruptedUnfinishedAuditShowsUnknownOutcomeAndExplicitRecovery()
    {
        var item = new AuditExecutionViewModel(CreateRecord());
        Assert.Equal("interrupted", item.OutcomeKind);
        Assert.Equal("已中断", item.OutcomeLabel);
        Assert.Equal("执行结果未知", item.DurationLabel);
        Assert.Contains("可能已发生", item.Detail);
        Assert.Contains("核实实际状态", item.Detail);
        Assert.Contains("不会自动重放", item.Detail);
        Assert.DoesNotContain("synthetic-secret", item.Detail + item.Metadata);
        Assert.Null(item.Record.IsSuccess);
        Assert.Null(item.Record.ApprovalGranted);
    }

    [Theory]
    [InlineData(true, "success")]
    [InlineData(false, "failed")]
    public void ActualCompletionRemainsAuthoritativeAfterAnInterruption(bool success, string kind)
    {
        var record = CreateRecord();
        var item = new AuditExecutionViewModel(record with
        { CompletedAt = record.StartedAt.AddSeconds(2), IsSuccess = success, ApprovalGranted = true });
        Assert.Equal(kind, item.OutcomeKind);
        Assert.DoesNotContain("未知", item.DurationLabel);
        Assert.DoesNotContain("未收到完成结果", item.Detail);
    }

    [Fact]
    public void CurrentLiveAuditRemainsPending()
    {
        var item = new AuditExecutionViewModel(CreateRecord() with { InterruptedAt = null });
        Assert.Equal("pending", item.OutcomeKind);
        Assert.Equal("进行中", item.OutcomeLabel);
        Assert.False(item.HasDetail);
    }

    private static ToolExecutionAuditRecord CreateRecord() => new(Guid.NewGuid(), Guid.NewGuid(), "call", "browser.navigate",
        ToolRiskLevel.ExternalSideEffect, ApprovalPolicy.AskEveryTime, DateTimeOffset.UnixEpoch,
        null, null, null, "synthetic-secret", null, 0, DateTimeOffset.UnixEpoch.AddSeconds(1));
}
