using CodingAgent.Api.Contracts.Skills;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/skills")]
public class SkillsController(ISkillRepository skills) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SkillDto>>> GetAll([FromQuery] bool enabledOnly = false, CancellationToken ct = default)
    {
        var list = await skills.GetAllAsync(enabledOnly, ct);
        return Ok(list.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SkillDto>> Get(int id, CancellationToken ct)
    {
        var skill = await skills.GetAsync(id, ct);
        return skill is null ? NotFound() : Ok(ToDto(skill));
    }

    [HttpPost]
    public async Task<ActionResult<SkillDto>> Create([FromBody] CreateSkillRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Instructions))
        {
            return BadRequest(new { message = "name 和 instructions 不能为空" });
        }
        if (await skills.GetByNameAsync(request.Name, ct) is not null)
        {
            return Conflict(new { message = $"技能名已存在: {request.Name}" });
        }
        var skill = new Skill
        {
            Name = request.Name,
            Description = request.Description,
            Instructions = request.Instructions,
            Enabled = request.Enabled,
        };
        await skills.AddAsync(skill, ct);
        return Created($"/api/skills/{skill.Id}", ToDto(skill));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateSkillRequest request, CancellationToken ct)
    {
        var skill = await skills.GetAsync(id, ct);
        if (skill is null) return NotFound();
        skill.Name = request.Name;
        skill.Description = request.Description;
        skill.Instructions = request.Instructions;
        skill.Enabled = request.Enabled;
        await skills.UpdateAsync(skill, ct);
        return Ok(ToDto(skill));
    }

    [HttpPatch("{id:int}/toggle")]
    public async Task<IActionResult> Toggle(int id, CancellationToken ct)
    {
        var skill = await skills.GetAsync(id, ct);
        if (skill is null) return NotFound();
        skill.Enabled = !skill.Enabled;
        await skills.UpdateAsync(skill, ct);
        return Ok(ToDto(skill));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await skills.DeleteAsync(id, ct);
        return NoContent();
    }

    internal static SkillDto ToDto(Skill s) => new(s.Id, s.Name, s.Description, s.Instructions, s.Enabled);
}
