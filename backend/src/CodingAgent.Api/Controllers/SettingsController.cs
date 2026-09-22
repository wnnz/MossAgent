using CodingAgent.Api.Contracts.Settings;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/settings")]
public class SettingsController(ISettingsRepository settings) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SettingDto>>> GetAll(CancellationToken ct)
    {
        var list = await settings.GetAllAsync(ct);
        // 不暴露 jwt_secret 等内部键
        return Ok(list.Where(s => !s.Key.StartsWith("jwt_", StringComparison.OrdinalIgnoreCase)).Select(ToDto).ToList());
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UpdateSettingRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Key))
        {
            return BadRequest(new { message = "key 不能为空" });
        }
        if (request.Key.StartsWith("jwt_", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "内部设置不允许修改" });
        }
        await settings.SetAsync(request.Key, request.Value ?? string.Empty, ct);
        return Ok(new { key = request.Key, value = request.Value });
    }

    internal static SettingDto ToDto(Domain.Entities.AppSetting s) => new(s.Key, s.Value);
}
