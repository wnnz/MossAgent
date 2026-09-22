using System.Text.Json;

namespace CodingAgent.Api.Middleware;

/// <summary>全局异常 → 500 JSON {message}。</summary>
public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "未处理异常: {Path}", context.Request.Path);
            if (context.Response.HasStarted)
            {
                throw;
            }
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message = ex.Message }));
        }
    }
}
