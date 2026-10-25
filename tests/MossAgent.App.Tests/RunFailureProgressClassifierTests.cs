using MossAgent.Application.Agent;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class RunFailureProgressClassifierTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Theory]
    [InlineData(TaskFailureReason.UnsupportedImageInput)]
    [InlineData(TaskFailureReason.InvalidModelParameters)]
    [InlineData(TaskFailureReason.UnsupportedReasoningEffort)]
    [InlineData(TaskFailureReason.AttachmentHistory)]
    [InlineData(TaskFailureReason.ModelContextCapacity)]
    [InlineData(TaskFailureReason.WorkspaceAuthorizationChanged)]
    [InlineData(TaskFailureReason.ProxyAuthentication)]
    [InlineData(TaskFailureReason.ProviderConnection)]
    public void PreflightFailures_AreNotSubmittedWithoutToolOverride(TaskFailureReason reason)
    {
        Assert.Equal(RunFailureProgress.NotSubmitted, RunFailureProgressClassifier.Classify(reason, false, false));
        Assert.Equal(RunFailureProgress.NotSubmitted, RunFailureProgressClassifier.Classify(reason, true, false));
        Assert.Equal(RunFailureProgress.ToolsExecuted, RunFailureProgressClassifier.Classify(reason, true, true));
    }

    [Theory]
    [InlineData(TaskFailureReason.ProviderHttp)]
    [InlineData(TaskFailureReason.InvalidProviderResponse)]
    [InlineData(TaskFailureReason.ProviderRateLimit)]
    [InlineData(TaskFailureReason.ProviderBlocked)]
    [InlineData(TaskFailureReason.ProviderOutputLimit)]
    [InlineData(TaskFailureReason.ProviderAuthentication)]
    [InlineData(TaskFailureReason.ProviderTimeout)]
    [InlineData(TaskFailureReason.StreamLineLimit)]
    [InlineData(TaskFailureReason.StreamEventLimit)]
    [InlineData(TaskFailureReason.InvalidStreamEncoding)]
    public void StreamAndResponseFailures_ArePossiblySubmittedWithoutToolOverride(TaskFailureReason reason)
    {
        Assert.Equal(RunFailureProgress.PossiblySubmitted, RunFailureProgressClassifier.Classify(reason, false, false));
        Assert.Equal(RunFailureProgress.PossiblySubmitted, RunFailureProgressClassifier.Classify(reason, true, false));
        Assert.Equal(RunFailureProgress.ToolsExecuted, RunFailureProgressClassifier.Classify(reason, false, true));
    }

    [Fact]
    public void TurnLimit_RequiresObservedStreamOutput()
    {
        Assert.Equal(RunFailureProgress.Unknown, RunFailureProgressClassifier.Classify(TaskFailureReason.TurnLimit, false, false));
        Assert.Equal(RunFailureProgress.PossiblySubmitted, RunFailureProgressClassifier.Classify(TaskFailureReason.TurnLimit, true, false));
        Assert.Equal(RunFailureProgress.ToolsExecuted, RunFailureProgressClassifier.Classify(TaskFailureReason.TurnLimit, false, true));
    }

    [Theory]
    [InlineData(null)]
    [InlineData(TaskFailureReason.Unknown)]
    [InlineData((TaskFailureReason)int.MaxValue)]
    public void UnknownOrLegacyReasons_StayUnknownWithoutObservedOutput(TaskFailureReason? reason)
    {
        Assert.Equal(RunFailureProgress.Unknown, RunFailureProgressClassifier.Classify(reason, false, false));
        Assert.Equal(RunFailureProgress.Unknown, RunFailureProgressClassifier.Classify(reason, true, false));
        Assert.Equal(RunFailureProgress.ToolsExecuted, RunFailureProgressClassifier.Classify(reason, true, true));
    }

    [Theory]
    [InlineData("provider_connection", RunFailureProgress.NotSubmitted)]
    [InlineData("model_context_capacity", RunFailureProgress.NotSubmitted)]
    [InlineData("proxy_authentication_failed", RunFailureProgress.NotSubmitted)]
    [InlineData("http_error", RunFailureProgress.PossiblySubmitted)]
    [InlineData("invalid_tool_arguments", RunFailureProgress.PossiblySubmitted)]
    [InlineData("provider_timeout", RunFailureProgress.PossiblySubmitted)]
    [InlineData("invalid_api_key", RunFailureProgress.PossiblySubmitted)]
    [InlineData("turn_limit", RunFailureProgress.Unknown)]
    [InlineData("Bearer synthetic-secret", RunFailureProgress.Unknown)]
    [InlineData(null, RunFailureProgress.Unknown)]
    public void RawCodes_ClassifyThroughTheSameMapping(string? code, RunFailureProgress expected) =>
        Assert.Equal(expected, RunFailureProgressClassifier.Classify(code, false, false));

    [Fact]
    public void HistoryClassification_CountsOnlyOutputAfterTheLastUserMessage()
    {
        var taskId = Guid.NewGuid();
        var history = new ConversationMessage[]
        {
            new(Guid.NewGuid(), taskId, MessageRole.User, "first prompt", Now),
            new(Guid.NewGuid(), taskId, MessageRole.Tool, "tool from earlier turn", Now, "call-1"),
            new(Guid.NewGuid(), taskId, MessageRole.Assistant, "earlier answer", Now),
            new(Guid.NewGuid(), taskId, MessageRole.User, "retry prompt", Now),
            new(Guid.NewGuid(), taskId, MessageRole.Assistant, "  ", Now),
        };
        Assert.Equal(RunFailureProgress.NotSubmitted,
            RunFailureProgressClassifier.Classify(TaskFailureReason.ProviderConnection, history));
        Assert.Equal(RunFailureProgress.Unknown,
            RunFailureProgressClassifier.Classify(TaskFailureReason.TurnLimit, history));
    }

    [Fact]
    public void HistoryClassification_ToolAndContentAfterLastUserMessageDriveProgress()
    {
        var taskId = Guid.NewGuid();
        var user = new ConversationMessage(Guid.NewGuid(), taskId, MessageRole.User, "prompt", Now);
        Assert.Equal(RunFailureProgress.ToolsExecuted, RunFailureProgressClassifier.Classify(
            TaskFailureReason.ProviderConnection, [user,
                new(Guid.NewGuid(), taskId, MessageRole.Tool, "{}", Now, "call")]));
        Assert.Equal(RunFailureProgress.PossiblySubmitted, RunFailureProgressClassifier.Classify(
            TaskFailureReason.TurnLimit, [user,
                new(Guid.NewGuid(), taskId, MessageRole.Assistant, "partial", Now)]));
        Assert.Equal(RunFailureProgress.Unknown, RunFailureProgressClassifier.Classify(
            TaskFailureReason.TurnLimit, [user,
                new(Guid.NewGuid(), taskId, MessageRole.System, "notice", Now)]));
    }
}
