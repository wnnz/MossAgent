using System.Net;
using System.Net.Http;
using MossAgent.Domain;
using MossAgent.Application.Attachments;
using MossAgent.Application.Models;

namespace MossAgent.Application.Agent;

public static class TaskFailureClassifier
{
    public static TaskFailureReason FromCode(string? code) => code switch
    {
        "stream_line_limit" => TaskFailureReason.StreamLineLimit,
        "stream_event_limit" => TaskFailureReason.StreamEventLimit,
        "invalid_stream_encoding" => TaskFailureReason.InvalidStreamEncoding,
        "http_error" => TaskFailureReason.ProviderHttp,
        "proxy_authentication_failed" => TaskFailureReason.ProxyAuthentication,
        "invalid_response" or "incomplete_response" or "invalid_tool_arguments"
            or "tool_calls_limit" or "tool_arguments_limit" => TaskFailureReason.InvalidProviderResponse,
        "turn_limit" => TaskFailureReason.TurnLimit,
        "provider_connection" => TaskFailureReason.ProviderConnection,
        "provider_timeout" => TaskFailureReason.ProviderTimeout,
        "invalid_api_key" or "authentication_error" or "UNAUTHENTICATED" => TaskFailureReason.ProviderAuthentication,
        "rate_limit_exceeded" or "rate_limit_error" or "RESOURCE_EXHAUSTED" => TaskFailureReason.ProviderRateLimit,
        "provider_blocked" => TaskFailureReason.ProviderBlocked,
        "output_limit" => TaskFailureReason.ProviderOutputLimit,
        "unsupported_reasoning_effort" => TaskFailureReason.UnsupportedReasoningEffort,
        ModelCapabilityException.UnsupportedImageInput => TaskFailureReason.UnsupportedImageInput,
        ModelCapabilityException.InvalidModelParameters => TaskFailureReason.InvalidModelParameters,
        "attachment_history_unavailable" => TaskFailureReason.AttachmentHistory,
        "model_context_capacity" => TaskFailureReason.ModelContextCapacity,
        "workspace_authorization_changed" => TaskFailureReason.WorkspaceAuthorizationChanged,
        _ => TaskFailureReason.Unknown
    };

    public static string SafeCode(string? code) => FromCode(code) == TaskFailureReason.Unknown ? "run_failed" : code!;

    public static string CodeFromException(Exception exception) => exception switch
    {
        ModelCapabilityException capability => capability.Code,
        ConversationAttachmentUnavailableException => "attachment_history_unavailable",
        PinnedModelContextException => "model_context_capacity",
        WorkspaceAuthorizationException => "workspace_authorization_changed",
        HttpRequestException { StatusCode: HttpStatusCode.ProxyAuthenticationRequired } => "proxy_authentication_failed",
        HttpRequestException => "provider_connection",
        TimeoutException or OperationCanceledException => "provider_timeout",
        _ => "run_failed"
    };

    public static TaskFailureReason? Normalize(AgentTaskStatus status, TaskFailureReason? reason) =>
        status != AgentTaskStatus.Failed ? null
        : reason is { } value && !Enum.IsDefined(value) ? TaskFailureReason.Unknown : reason;
}
