using CodingAgent.Api.Contracts.Sessions;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController(ISessionRepository sessions, IProviderRepository providers) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SessionDto>>> GetAll([FromQuery] bool includeArchived = false, CancellationToken ct = default)
    {
        var list = await sessions.GetAllAsync(includeArchived, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDto>> Get(Guid id, CancellationToken ct)
    {
        var session = await sessions.GetAsync(id, ct);
        return session is null ? NotFound() : Ok(ToDto(session));
    }

    [HttpPost]
    public async Task<ActionResult<SessionDto>> Create([FromBody] CreateSessionRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ModelId))
        {
            return BadRequest(new { message = "modelId 不能为空" });
        }
        if (await providers.GetAsync(request.ProviderId, ct) is null)
        {
            return BadRequest(new { message = "提供商不存在" });
        }
        var now = DateTime.UtcNow;
        var session = new Session
        {
            Id = Guid.NewGuid(),
            Title = string.IsNullOrWhiteSpace(request.Title) ? "新会话" : request.Title,
            ProviderId = request.ProviderId,
            ModelId = request.ModelId,
            ReasoningEffort = ParseEffort(request.ReasoningEffort),
            WorkspacePath = string.IsNullOrWhiteSpace(request.WorkspacePath) ? null : request.WorkspacePath,
            CreatedAt = now,
            UpdatedAt = now,
        };
        await sessions.AddAsync(session, ct);
        return Created($"/api/sessions/{session.Id}", ToDto(session));
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSessionRequest request, CancellationToken ct)
    {
        var session = await sessions.GetAsync(id, ct);
        if (session is null) return NotFound();
        if (request.Title is not null) session.Title = request.Title;
        if (request.ProviderId.HasValue) session.ProviderId = request.ProviderId.Value;
        if (request.ModelId is not null) session.ModelId = request.ModelId;
        if (request.ReasoningEffort is not null) session.ReasoningEffort = ParseEffort(request.ReasoningEffort);
        if (request.WorkspacePath is not null) session.WorkspacePath = string.IsNullOrWhiteSpace(request.WorkspacePath) ? null : request.WorkspacePath;
        if (request.Status is not null && Enum.TryParse<SessionStatus>(request.Status, true, out var status)) session.Status = status;
        session.UpdatedAt = DateTime.UtcNow;
        await sessions.UpdateAsync(session, ct);
        return Ok(ToDto(session));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sessions.DeleteAsync(id, ct);
        await sessions.DeleteMessagesAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/messages")]
    public async Task<ActionResult<List<ChatMessageDto>>> GetMessages(Guid id, CancellationToken ct)
    {
        if (await sessions.GetAsync(id, ct) is null) return NotFound();
        var messages = await sessions.GetMessagesAsync(id, ct);
        return Ok(messages.Select(ToMessageDto).ToList());
    }

    private static ReasoningEffort ParseEffort(string effort) =>
        Enum.TryParse<ReasoningEffort>(effort, true, out var parsed) ? parsed : ReasoningEffort.Off;

    internal static SessionDto ToDto(Session session) => new(
        session.Id,
        session.Title,
        session.ProviderId,
        session.ModelId,
        ToSnake(session.ReasoningEffort),
        session.WorkspacePath,
        ToSnake(session.Status),
        session.CreatedAt,
        session.UpdatedAt);

    internal static ChatMessageDto ToMessageDto(ChatMessage message) => new(
        message.Id,
        message.SessionId,
        ToSnake(message.Role),
        message.Content,
        message.ToolCallsJson,
        message.ToolCallId,
        message.Name,
        message.CreatedAt);

    private static string ToSnake(Enum value) => Sse.EnumSnakeCase.ToSnake(value);
}
