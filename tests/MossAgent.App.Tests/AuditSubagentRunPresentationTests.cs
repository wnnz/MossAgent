using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuditSubagentRunPresentationTests
{
    private const string Snapshot = """{"profile":"coder","systemPromptPreview":"p","systemPromptLength":1,"providerId":"00000000-0000-0000-0000-000000000001","providerProtocol":"openai-responses","modelProfileId":"00000000-0000-0000-0000-000000000002","modelId":"gpt-5","reasoningEffort":"high","shareContext":true,"taskPreview":"重写认证模块","taskLength":6,"sharedImageCount":0}""";
    private const string SnapshotNoShare = """{"providerProtocol":"anthropic-messages","modelId":"claude-sonnet","reasoningEffort":"low","shareContext":false,"taskPreview":"整理文档"}""";
    private static readonly DateTimeOffset Created = new(2026, 10, 9, 0, 0, 0, TimeSpan.Zero);

    private static SubagentRunRecord Run(SubagentRunStatus status, string snapshot = Snapshot) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "call-1", "代码助手", snapshot, status, null,
            Created, status == SubagentRunStatus.Running ? Created : Created.AddMilliseconds(240));

    [Theory]
    [InlineData(SubagentRunStatus.Running, "running", "运行中", true)]
    [InlineData(SubagentRunStatus.Completed, "success", "成功", false)]
    [InlineData(SubagentRunStatus.Cancelled, "cancelled", "已取消", true)]
    [InlineData(SubagentRunStatus.Interrupted, "interrupted", "已中断", true)]
    [InlineData(SubagentRunStatus.Failed, "failed", "失败", true)]
    public void JournalStatusMapsToSharedOutcomeKindAndLocalizedLabel(SubagentRunStatus status,
        string expectedKind, string expectedLabel, bool hasHint)
    {
        var item = new AuditSubagentRunViewModel(Run(status));
        Assert.Equal(expectedKind, item.OutcomeKind);
        Assert.Equal(expectedLabel, item.OutcomeLabel);
        Assert.Contains("gpt-5", item.Detail);
        Assert.Equal(hasHint, item.Detail.Contains('\n'));
    }

    [Fact]
    public void DetailCombinesLocalizedHintWithDefensiveSnapshotSummary()
    {
        var item = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Failed));
        Assert.Contains("子代理运行未完成", item.Detail);
        Assert.Contains("openai-responses", item.Detail);
        Assert.Contains("gpt-5", item.Detail);
        Assert.Contains("high", item.Detail);
        Assert.Contains("共享上下文", item.Detail);
        Assert.Contains("重写认证模块", item.Detail);
    }

    [Fact]
    public void SuccessfulRunDetailShowsOnlyTheSnapshotSummary()
    {
        var item = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Completed, SnapshotNoShare));
        Assert.Contains("claude-sonnet", item.Detail);
        Assert.Contains("独立上下文", item.Detail);
        Assert.DoesNotContain("请检查", item.Detail);
        Assert.DoesNotContain("子代理运行", item.Detail);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[1,2]")]
    [InlineData("{\"providerProtocol\":")]
    [InlineData("")]
    public void MalformedSnapshotFallsBackToProfileNameAndStatus(string snapshot)
    {
        var item = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Failed, snapshot));
        Assert.Equal("代码助手 · 失败", item.Detail);
    }

    [Fact]
    public void SnapshotFieldsAreSanitizedAndBoundedForDisplay()
    {
        const string dirty = """{"providerProtocol":"proto\u0001col","modelId":"m","reasoningEffort":"","shareContext":false,"taskPreview":"x"}""";
        var item = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Completed, dirty));
        Assert.Contains("protocol", item.Detail);
        Assert.Contains("独立上下文", item.Detail);
        Assert.Contains("—", item.Detail);
        Assert.DoesNotContain('\u0001', item.Detail);
    }

    [Fact]
    public void ProfileNameLimitDoesNotSplitASurrogatePair()
    {
        var record = Run(SubagentRunStatus.Completed) with { ProfileName = new string('x', 159) + "😀" };
        Assert.Equal(new string('x', 159), new AuditSubagentRunViewModel(record).ProfileName);
    }

    [Fact]
    public void MetadataUsesLocalTimeAndPendingLabelWhileRunning()
    {
        var running = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Running));
        Assert.Equal("进行中", running.DurationLabel);
        var completed = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Completed));
        Assert.Equal("耗时 240 毫秒", completed.DurationLabel);
        var reversed = new AuditSubagentRunViewModel(Run(SubagentRunStatus.Completed) with
        { UpdatedAt = Created.AddSeconds(-1) });
        Assert.Equal("耗时未知或尚未结束", reversed.DurationLabel);
    }
}
