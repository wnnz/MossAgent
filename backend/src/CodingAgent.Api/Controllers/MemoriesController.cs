using CodingAgent.Api.Contracts.Memories;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/memories")]
public class MemoriesController(IMemoryRepository memories) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MemoryDto>>> GetAll([FromQuery] string? scope = null, CancellationToken ct = default)
    {
        var parsed = Enum.TryParse<MemoryScope>(scope, true, out var s) ? s : (MemoryScope?)null;
        var list = await memories.GetAllAsync(parsed, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<MemoryDto>>> Search([FromQuery] string q, CancellationToken ct)
    {
        var list = await memories.SearchAsync(q ?? string.Empty, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<MemoryDto>> Get(int id, CancellationToken ct)
    {
        var item = await memories.GetAsync(id, ct);
        return item is null ? NotFound() : Ok(ToDto(item));
    }

    [HttpPost]
    public async Task<ActionResult<MemoryDto>> Create([FromBody] CreateMemoryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Content))
        {
            return BadRequest(new { message = "title 和 content 不能为空" });
        }
        var item = new MemoryItem
        {
            Scope = ParseScope(request.Scope),
            Title = request.Title,
            Content = request.Content,
            Tags = request.Tags,
        };
        await memories.AddAsync(item, ct);
        return Created($"/api/memories/{item.Id}", ToDto(item));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMemoryRequest request, CancellationToken ct)
    {
        var item = await memories.GetAsync(id, ct);
        if (item is null) return NotFound();
        item.Scope = ParseScope(request.Scope);
        item.Title = request.Title;
        item.Content = request.Content;
        item.Tags = request.Tags;
        await memories.UpdateAsync(item, ct);
        return Ok(ToDto(item));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await memories.DeleteAsync(id, ct);
        return NoContent();
    }

    private static MemoryScope ParseScope(string scope) =>
        scope.Contains("project", StringComparison.OrdinalIgnoreCase) ? MemoryScope.Project : MemoryScope.Global;

    internal static MemoryDto ToDto(MemoryItem m) => new(
        m.Id,
        m.Scope == MemoryScope.Project ? "project" : "global",
        m.Title,
        m.Content,
        m.Tags,
        m.UpdatedAt);
}
