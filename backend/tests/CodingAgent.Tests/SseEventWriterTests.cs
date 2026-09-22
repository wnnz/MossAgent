using System.Text;
using CodingAgent.Api.Sse;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Models;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace CodingAgent.Tests;

public class SseEventWriterTests
{
    [Fact]
    public async Task WriteAsync_FormatsEventAndData()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        await SseEventWriter.WriteAsync(context.Response, "message_delta", new { content = "你好" }, CancellationToken.None);

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("event: message_delta\n", text);
        Assert.Contains("data: {\"content\":\"你好\"}\n\n", text);
    }

    [Fact]
    public async Task WriteAsync_SerializesEnumAsSnakeCase()
    {
        var context = new DefaultHttpContext();
        var body = new MemoryStream();
        context.Response.Body = body;

        var evt = new { type = ProviderType.OpenAiCompatible };
        await SseEventWriter.WriteAsync(context.Response, "test", evt, CancellationToken.None);

        var text = Encoding.UTF8.GetString(body.ToArray());
        Assert.Contains("openai_compatible", text);
    }

    [Fact]
    public void SnakeCase_ConvertsSimpleEnum()
    {
        Assert.Equal("socks5", LowerSnakeCaseEnumConverter<ProxyScheme>.ToSnakeCase("Socks5"));
        Assert.Equal("off", LowerSnakeCaseEnumConverter<ReasoningEffort>.ToSnakeCase("Off"));
        Assert.Equal("medium", LowerSnakeCaseEnumConverter<ReasoningEffort>.ToSnakeCase("Medium"));
        Assert.Equal("openai_compatible", LowerSnakeCaseEnumConverter<ProviderType>.ToSnakeCase("OpenAiCompatible"));
    }
}
