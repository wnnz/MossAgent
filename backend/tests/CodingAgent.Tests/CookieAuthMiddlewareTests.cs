using CodingAgent.Api.Middleware;
using CodingAgent.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CodingAgent.Tests;

/// <summary>认证门禁测试：DefaultHttpContext + fake ITokenService 直接实例化中间件。</summary>
public class CookieAuthMiddlewareTests
{
    private sealed class FakeTokenService(bool valid) : ITokenService
    {
        public string IssueToken(TimeSpan lifetime) => "token";
        public bool TryValidate(string token) => valid;
    }

    private static CookieAuthMiddleware CreateMiddleware(bool called = false)
    {
        var nextInvoked = called;
        return new CookieAuthMiddleware(_ => { nextInvoked = true; return Task.CompletedTask; });
    }

    private static DefaultHttpContext CreateContext(string path, string? cookie = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (cookie is not null)
        {
            context.Request.Headers.Cookie = cookie;
        }
        return context;
    }

    private static async Task<(int Status, bool NextCalled)> InvokeAsync(CookieAuthMiddleware middleware, DefaultHttpContext context)
    {
        var nextCalled = false;
        var wrapped = new CookieAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        var services = new ServiceCollection();
        services.AddSingleton<ITokenService>(new FakeTokenService(false));
        context.RequestServices = services.BuildServiceProvider();
        await wrapped.InvokeAsync(context);
        return (context.Response.StatusCode, nextCalled);
    }

    [Fact]
    public async Task AnonymousPaths_PassThroughWithoutAuth()
    {
        foreach (var path in new[] { "/api/auth/status", "/api/auth/setup", "/api/auth/login", "/api/auth/login/" })
        {
            var context = CreateContext(path);
            var (status, nextCalled) = await InvokeAsync(null!, context);
            Assert.Equal(200, status);
            Assert.True(nextCalled);
        }
    }

    [Fact]
    public async Task ProtectedApi_WithoutCookie_Returns401()
    {
        var context = CreateContext("/api/providers");
        var (status, nextCalled) = await InvokeAsync(null!, context);
        Assert.Equal(401, status);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task ProtectedApi_WithInvalidCookie_Returns401()
    {
        var context = CreateContext("/api/providers", "codingagent_auth=bad-token");
        var (status, nextCalled) = await InvokeAsync(null!, context);
        Assert.Equal(401, status);
        Assert.False(nextCalled);
    }

    [Fact]
    public async Task ProtectedApi_WithValidCookie_PassesThrough()
    {
        var context = CreateContext("/api/providers", "codingagent_auth=valid");
        var nextCalled = false;
        var middleware = new CookieAuthMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });
        // 服务容器提供有效 token 校验
        var services = new ServiceCollection();
        services.AddSingleton<ITokenService>(new FakeTokenService(true));
        context.RequestServices = services.BuildServiceProvider();

        await middleware.InvokeAsync(context);
        Assert.Equal(200, context.Response.StatusCode);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task NonApiPaths_PassThroughWithoutAuth()
    {
        var context = CreateContext("/index.html");
        var (status, nextCalled) = await InvokeAsync(null!, context);
        Assert.Equal(200, status);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task PutPassword_RequiresAuth()
    {
        // 改密端点不在匿名列表：无 cookie 必须 401
        var context = CreateContext("/api/auth/password");
        var (status, nextCalled) = await InvokeAsync(null!, context);
        Assert.Equal(401, status);
        Assert.False(nextCalled);
    }
}
