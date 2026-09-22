using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Security;

namespace CodingAgent.Api.Middleware;

/// <summary>JWT Cookie 认证：校验 Cookie 并放行匿名端点，其余 /api 请求未认证返回 401。</summary>
public class CookieAuthMiddleware(RequestDelegate next)
{
    private static readonly string[] AnonymousPaths =
    [
        "/api/auth/status",
        "/api/auth/setup",
        "/api/auth/login",
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            // 容忍尾斜杠
            var normalized = path.TrimEnd('/').ToLowerInvariant();
            var isAnonymous = AnonymousPaths.Any(p => normalized == p);

            if (!isAnonymous && !IsAuthenticated(context))
            {
                context.Response.StatusCode = 401;
                await context.Response.WriteAsJsonAsync(new { message = "未认证" });
                return;
            }
        }

        await next(context);
    }

    private static bool IsAuthenticated(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(JwtCookieTokenService.CookieName, out var token) || string.IsNullOrEmpty(token))
        {
            return false;
        }
        var tokenService = context.RequestServices.GetRequiredService<ITokenService>();
        return tokenService.TryValidate(token);
    }
}
