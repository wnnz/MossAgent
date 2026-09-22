using CodingAgent.Api.Contracts.SubAgents;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/subagents")]
public class SubAgentsController(ISubAgentRepository subAgents) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SubAgentDto>>> GetAll([FromQuery] bool enabledOnly = false, CancellationToken ct = default)
    {
        var list = await subAgents.GetAllAsync(enabledOnly, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SubAgentDto>> Get(int id, CancellationToken ct)
    {
        var subAgent = await subAgents.GetAsync(id, ct);
        return subAgent is null ? NotFound() : Ok(ToDto(subAgent));
    }

    [HttpPost]
    public async Task<ActionResult<SubAgentDto>> Create([FromBody] CreateSubAgentRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.ModelId))
        {
            return BadRequest(new { message = "name 和 modelId 不能为空" });
        }
        var subAgent = new SubAgent
        {
            Name = request.Name,
            Description = request.Description,
            InvocationRule = request.InvocationRule,
            SystemPrompt = request.SystemPrompt,
            ProviderId = request.ProviderId,
            ModelId = request.ModelId,
            ReasoningEffort = ParseEffort(request.ReasoningEffort),
            MaxTurns = request.MaxTurns,
            AllowedToolsJson = request.AllowedToolsJson,
            Enabled = request.Enabled,
        };
        await subAgents.AddAsync(subAgent, ct);
        return Created($"/api/subagents/{subAgent.Id}", ToDto(subAgent));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSubAgentRequest request, CancellationToken ct)
    {
        var subAgent = await subAgents.GetAsync(id, ct);
        if (subAgent is null) return NotFound();
        subAgent.Name = request.Name;
        subAgent.Description = request.Description;
        subAgent.InvocationRule = request.InvocationRule;
        subAgent.SystemPrompt = request.SystemPrompt;
        subAgent.ProviderId = request.ProviderId;
        subAgent.ModelId = request.ModelId;
        subAgent.ReasoningEffort = ParseEffort(request.ReasoningEffort);
        subAgent.MaxTurns = request.MaxTurns;
        subAgent.AllowedToolsJson = request.AllowedToolsJson;
        subAgent.Enabled = request.Enabled;
        await subAgents.UpdateAsync(subAgent, ct);
        return Ok(ToDto(subAgent));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await subAgents.DeleteAsync(id, ct);
        return NoContent();
    }

    private static ReasoningEffort ParseEffort(string effort) =>
        Enum.TryParse<ReasoningEffort>(effort, true, out var parsed) ? parsed : ReasoningEffort.Off;

    internal static SubAgentDto ToDto(SubAgent s) => new(
        s.Id,
        s.Name,
        s.Description,
        s.InvocationRule,
        s.SystemPrompt,
        s.ProviderId,
        s.ModelId,
        Sse.EnumSnakeCase.ToSnake(s.ReasoningEffort),
        s.MaxTurns,
        s.AllowedToolsJson,
        s.Enabled);
}
