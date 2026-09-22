using CodingAgent.Api.Contracts.Chat;
using CodingAgent.Api.Sse;
using CodingAgent.Agent;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Storage;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class AgentChatController(
    ISessionRepository sessions,
    AgentLoop agentLoop,
    IWorkspaceLocator workspaceLocator,
    SessionCancellationMap cancellationMap) : ControllerBase
{
    [HttpPost("{id:guid}/chat")]
    public async Task Chat(Guid id, [FromBody] ChatRequest request, CancellationToken requestAborted)
    {
        var session = await sessions.GetAsync(id, requestAborted);
        if (session is null)
        {
            Response.StatusCode = 404;
            await Response.WriteAsJsonAsync(new { message = "会话不存在" });
            return;
        }
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = 400;
            await Response.WriteAsJsonAsync(new { message = "message 不能为空" });
            return;
        }

        var workspacePath = session.WorkspacePath ?? workspaceLocator.WorkspaceRoot;
        ReasoningEffort? effortOverride = request.ReasoningEffort is { Length: > 0 } raw
            && Enum.TryParse<ReasoningEffort>(raw, true, out var parsed)
            ? parsed
            : null;

        // 同一会话已有运行中的 chat 时拒绝并发
        if (cancellationMap.IsRunning(id))
        {
            Response.StatusCode = 409;
            await Response.WriteAsJsonAsync(new { message = "该会话已有进行中的对话" });
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no";

        var cts = cancellationMap.Start(id);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, requestAborted);

        try
        {
            session.WorkspacePath = session.WorkspacePath ?? workspacePath;
            await foreach (var evt in agentLoop.RunAsync(session, request.Message, effortOverride, null, linked.Token))
            {
                if (linked.Token.IsCancellationRequested)
                {
                    break;
                }
                await SseEventWriter.WriteAsync(Response, ToEventName(evt.Type), evt.Data ?? new { }, linked.Token);
            }
        }
        catch (OperationCanceledException)
        {
            await SseEventWriter.WriteAsync(Response, "cancelled", new { }, CancellationToken.None);
        }
        catch (Exception ex)
        {
            if (!linked.Token.IsCancellationRequested)
            {
                try
                {
                    await SseEventWriter.WriteAsync(Response, "error", new { message = ex.Message }, CancellationToken.None);
                }
                catch { /* 响应已中断 */ }
            }
        }
        finally
        {
            cancellationMap.End(id, cts);
            // 刷新会话时间戳（消息已在 AgentLoop 中落库）
            var latest = await sessions.GetAsync(id, CancellationToken.None);
            if (latest is not null)
            {
                latest.UpdatedAt = DateTime.UtcNow;
                await sessions.UpdateAsync(latest, CancellationToken.None);
            }
        }
    }

    [HttpPost("{id:guid}/stop")]
    public async Task<IActionResult> Stop(Guid id)
    {
        var cancelled = cancellationMap.TryCancel(id);
        return Ok(new { cancelled });
    }

    private static string ToEventName(AgentEventType type) => type switch
    {
        AgentEventType.TurnStarted => "turn_started",
        AgentEventType.MessageDelta => "message_delta",
        AgentEventType.ToolCallStarted => "tool_call_started",
        AgentEventType.ToolCallFinished => "tool_call_finished",
        AgentEventType.TurnCompleted => "turn_completed",
        AgentEventType.Cancelled => "cancelled",
        AgentEventType.Error => "error",
        _ => "message_delta",
    };
}
