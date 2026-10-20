using System.Net.Http;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class TaskFailureClassifierTests
{
    [Theory]
    [InlineData("stream_line_limit", TaskFailureReason.StreamLineLimit)]
    [InlineData("stream_event_limit", TaskFailureReason.StreamEventLimit)]
    [InlineData("invalid_stream_encoding", TaskFailureReason.InvalidStreamEncoding)]
    [InlineData("http_error", TaskFailureReason.ProviderHttp)]
    [InlineData("proxy_authentication_failed", TaskFailureReason.ProxyAuthentication)]
    [InlineData("invalid_response", TaskFailureReason.InvalidProviderResponse)]
    [InlineData("incomplete_response", TaskFailureReason.InvalidProviderResponse)]
    [InlineData("invalid_tool_arguments", TaskFailureReason.InvalidProviderResponse)]
    [InlineData("tool_calls_limit", TaskFailureReason.InvalidProviderResponse)]
    [InlineData("tool_arguments_limit", TaskFailureReason.InvalidProviderResponse)]
    [InlineData("provider_blocked", TaskFailureReason.ProviderBlocked)]
    [InlineData("output_limit", TaskFailureReason.ProviderOutputLimit)]
    [InlineData("unsupported_reasoning_effort", TaskFailureReason.UnsupportedReasoningEffort)]
    [InlineData("attachment_history_unavailable", TaskFailureReason.AttachmentHistory)]
    [InlineData("model_context_capacity", TaskFailureReason.ModelContextCapacity)]
    [InlineData("turn_limit", TaskFailureReason.TurnLimit)]
    [InlineData("provider_connection", TaskFailureReason.ProviderConnection)]
    [InlineData("provider_timeout", TaskFailureReason.ProviderTimeout)]
    [InlineData("invalid_api_key", TaskFailureReason.ProviderAuthentication)]
    [InlineData("authentication_error", TaskFailureReason.ProviderAuthentication)]
    [InlineData("UNAUTHENTICATED", TaskFailureReason.ProviderAuthentication)]
    [InlineData("rate_limit_exceeded", TaskFailureReason.ProviderRateLimit)]
    [InlineData("rate_limit_error", TaskFailureReason.ProviderRateLimit)]
    [InlineData("RESOURCE_EXHAUSTED", TaskFailureReason.ProviderRateLimit)]
    public void KnownCodes_ProduceLocalizedBoundedDiagnostic(string code, TaskFailureReason reason)
    {
        Assert.Equal(reason, TaskFailureClassifier.FromCode(code));
        Assert.Equal(code, TaskFailureClassifier.SafeCode(code));
        var hint = WorkspaceFailureText.Get(reason);
        Assert.InRange(hint.Length, 10, 200);
        Assert.DoesNotContain(code, hint, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("STREAM_LINE_LIMIT")]
    [InlineData(" stream_line_limit ")]
    [InlineData("stream_line_limit: Bearer synthetic-secret")]
    [InlineData("<script>synthetic-secret</script>")]
    [InlineData("unsupported_reasoning_effort: fixture-secret")]
    [InlineData("proxy_authentication_failed: fixture-secret")]
    [InlineData("PROXY_AUTHENTICATION_FAILED")]
    [InlineData(" proxy_authentication_failed ")]
    [InlineData("attachment_history_unavailable: synthetic-secret")]
    [InlineData("model_context_capacity: synthetic-secret")]
    public void UnknownOrSpoofedCodes_NeverBecomeUserTextOrPersistedCodes(string? code)
    {
        Assert.Equal(TaskFailureReason.Unknown, TaskFailureClassifier.FromCode(code));
        Assert.Equal("run_failed", TaskFailureClassifier.SafeCode(code));
        Assert.Equal(WorkspaceText.Get("TaskFailureHint"), WorkspaceFailureText.Get(TaskFailureClassifier.FromCode(code)));
    }

    [Fact]
    public void UnknownAndLegacyCategories_UseSafeFallback()
    {
        foreach (var reason in new TaskFailureReason?[] { null, TaskFailureReason.Unknown, (TaskFailureReason)int.MaxValue })
            Assert.Equal(WorkspaceText.Get("TaskFailureHint"), WorkspaceFailureText.Get(reason));
    }

    [Fact]
    public void ExceptionClassification_UsesTypeRatherThanSensitiveMessages()
    {
        Assert.Equal("provider_connection", TaskFailureClassifier.CodeFromException(new HttpRequestException("Bearer synthetic-secret")));
        Assert.Equal("provider_timeout", TaskFailureClassifier.CodeFromException(new TimeoutException("Bearer synthetic-secret")));
        Assert.Equal("provider_timeout", TaskFailureClassifier.CodeFromException(new OperationCanceledException("Bearer synthetic-secret")));
        Assert.Equal("run_failed", TaskFailureClassifier.CodeFromException(new InvalidOperationException("Bearer synthetic-secret")));
        Assert.Equal("model_context_capacity", TaskFailureClassifier.CodeFromException(new PinnedModelContextException()));
    }

    [Fact]
    public void ProxyAuthenticationException_UsesOnlyTypedStatus()
    {
        var failure = new HttpRequestException("Bearer synthetic-secret", null,
            System.Net.HttpStatusCode.ProxyAuthenticationRequired);
        Assert.Equal("proxy_authentication_failed", TaskFailureClassifier.CodeFromException(failure));
        Assert.Equal("provider_connection", TaskFailureClassifier.CodeFromException(
            new HttpRequestException("HTTP 407 proxy_authentication_failed synthetic-secret")));
    }
}
