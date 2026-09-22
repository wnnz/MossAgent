using CodingAgent.Api.Contracts.Auth;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Security;
using Microsoft.AspNetCore.Mvc;

namespace CodingAgent.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthRepository authRepository, IPasswordHasher hasher, ITokenService tokens) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<AuthStatusResponse>> Status(CancellationToken ct)
    {
        var record = await authRepository.GetAsync(ct);
        var authenticated = Request.Cookies.TryGetValue(JwtCookieTokenService.CookieName, out var token)
            && !string.IsNullOrEmpty(token)
            && tokens.TryValidate(token);
        return Ok(new AuthStatusResponse(record is null, authenticated));
    }

    [HttpPost("setup")]
    public async Task<IActionResult> Setup([FromBody] SetupRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            return BadRequest(new { message = "密码至少 6 位" });
        }
        if (await authRepository.GetAsync(ct) is not null)
        {
            return Conflict(new { message = "密码已设置，不允许重复初始化" });
        }

        var salt = hasher.GenerateSalt();
        await authRepository.AddAsync(new AuthRecord
        {
            PasswordHash = hasher.Hash(request.Password, salt),
            Salt = salt,
            CreatedAt = DateTime.UtcNow,
        }, ct);

        SetCookie(tokens.IssueToken(TimeSpan.FromDays(7)));
        return Ok(new AuthStatusResponse(false, true));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var record = await authRepository.GetAsync(ct);
        if (record is null || !hasher.Verify(request.Password, record.Salt, record.PasswordHash))
        {
            return Unauthorized(new { message = "密码错误" });
        }
        SetCookie(tokens.IssueToken(TimeSpan.FromDays(request.RememberMe ? 30 : 7)));
        return Ok(new AuthStatusResponse(false, true));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(JwtCookieTokenService.CookieName);
        return Ok(new { message = "已登出" });
    }

    [HttpPut("password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.NewPassword) || request.NewPassword.Length < 6)
        {
            return BadRequest(new { message = "新密码至少 6 位" });
        }
        var record = await authRepository.GetAsync(ct);
        if (record is null)
        {
            return NotFound(new { message = "尚未设置密码" });
        }
        if (!hasher.Verify(request.CurrentPassword, record.Salt, record.PasswordHash))
        {
            return Unauthorized(new { message = "当前密码错误" });
        }

        var salt = hasher.GenerateSalt();
        record.Salt = salt;
        record.PasswordHash = hasher.Hash(request.NewPassword, salt);
        await authRepository.UpdateAsync(record, ct);
        return Ok(new { message = "密码已更新" });
    }

    private void SetCookie(string token)
    {
        // 本地单机场景：http 访问，不启用 Secure
        Response.Cookies.Append(JwtCookieTokenService.CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
        });
    }
}
