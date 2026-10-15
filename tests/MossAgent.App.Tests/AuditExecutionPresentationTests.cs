using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuditExecutionPresentationTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);
    private static ToolExecutionAuditRecord Record(bool? success, bool? approval, string? error, bool completed = true) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "call", "filesystem.read_file", ToolRiskLevel.ReadOnly,
            ApprovalPolicy.ReadOnly, Started, completed ? Started.AddMilliseconds(121) : null, approval, success, error, 1, 0);

    [Theory]
    [InlineData(true, true, null, true, "success", "成功")]
    [InlineData(null, null, null, false, "pending", "进行中")]
    [InlineData(false, true, "tool_cancelled", true, "cancelled", "已取消")]
    [InlineData(false, false, "approval_denied", true, "denied", "已拒绝")]
    [InlineData(false, true, "tool_timeout", true, "failed", "失败")]
    public void OutcomeDistinguishesPendingPermissionCancellationAndExecution(bool? success, bool? approval,
        string? error, bool completed, string expectedKind, string expectedLabel)
    {
        var item = new AuditExecutionViewModel(Record(success, approval, error, completed));
        Assert.Equal(expectedKind, item.OutcomeKind);
        Assert.Equal(expectedLabel, item.OutcomeLabel);
        Assert.Equal(expectedKind is not ("success" or "pending"), item.HasDetail);
    }

    [Theory]
    [InlineData("path_denied", "授权目录")]
    [InlineData("tool_timeout", "超时")]
    [InlineData("tool_schema_invalid", "参数")]
    [InlineData("invalid_arguments", "参数")]
    [InlineData("subagent_reasoning_unavailable", "子代理设置")]
    [InlineData("subagent_reasoning_unavailable?api_key=synthetic-secret", "配置")]
    [InlineData("secret-api-key://unexpected\r\nexception", "配置")]
    public void FailureHintsUseOnlyLocalizedAllowlistedCategories(string error, string expected)
    {
        var item = new AuditExecutionViewModel(Record(false, true, error));
        Assert.Contains(expected, item.Detail);
        Assert.DoesNotContain(error, item.Detail);
        Assert.DoesNotContain("secret", item.Detail);
    }

    [Theory]
    [InlineData(true, true, true, "success")]
    [InlineData(null, null, false, "pending")]
    [InlineData(false, false, true, "denied")]
    public void SavedConfigurationErrorDoesNotOverrideExecutionState(bool? success, bool? approval,
        bool completed, string expectedKind)
    {
        var item = new AuditExecutionViewModel(Record(success, approval, "subagent_reasoning_unavailable", completed));

        Assert.Equal(expectedKind, item.OutcomeKind);
        Assert.DoesNotContain("子代理设置", item.Detail);
        if (expectedKind == "denied") Assert.Contains("执行许可", item.Detail);
        else Assert.Empty(item.Detail);
    }

    [Fact]
    public void RejectedPathPointsToAuthorizedRootsInsteadOfPermissionEscalation()
    {
        var item = new AuditExecutionViewModel(Record(false, false, "path_denied"));
        Assert.Equal("denied", item.OutcomeKind);
        Assert.Contains("授权目录", item.Detail);
    }

    [Fact]
    public void ToolNameLimitDoesNotSplitASurrogatePair()
    {
        var item = new AuditExecutionViewModel(Record(true, true, null) with { ToolName = new string('x', 159) + "😀" });
        Assert.Equal(new string('x', 159), item.ToolName);
    }

    [Fact]
    public void MetadataUsesLocalTimeAndNonnegativeBoundedPresentation()
    {
        var item = new AuditExecutionViewModel(Record(true, true, "secret") with
        { ToolName = "tool\r\n" + new string('x', 200) });
        Assert.Equal(160, item.ToolName.Length);
        Assert.DoesNotContain('\r', item.ToolName);
        Assert.DoesNotContain('\n', item.ToolName);
        Assert.Equal("耗时 121 毫秒", item.DurationLabel);
        Assert.DoesNotContain("secret", item.Detail + item.Metadata);
        var invalid = new AuditExecutionViewModel(item.Record with { CompletedAt = Started.AddSeconds(-1) });
        Assert.DoesNotContain("-", invalid.DurationLabel);
    }
}
