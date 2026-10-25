using System.Net;
using MossAgent.Mcp;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class McpHttpFailureClassificationTests
{
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task RejectedRequest_ThrowsAuthenticationException(int status)
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))
        };
        await using var client = handler.CreateClient();

        var exception = await Assert.ThrowsAsync<McpAuthenticationException>(
            () => client.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal((HttpStatusCode)status, exception.StatusCode);
        Assert.Null(exception.ChallengeScheme);
    }

    [Fact]
    public async Task RejectedRequest_WithChallenge_CapturesScheme()
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (_, _) =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                response.Headers.TryAddWithoutValidation("WWW-Authenticate", "Bearer realm=\"mcp\"");
                return Task.FromResult(response);
            }
        };
        await using var client = handler.CreateClient();

        var exception = await Assert.ThrowsAsync<McpAuthenticationException>(
            () => client.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.Equal("Bearer", exception.ChallengeScheme);
    }

    [Fact]
    public async Task NotFound_AfterSessionWasAssigned_ThrowsSessionInvalid()
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (message, _) => Task.FromResult(
                message.GetProperty("method").GetString() == "tools/list"
                    ? new HttpResponseMessage(HttpStatusCode.NotFound)
                    : McpHttpTestHandler.DefaultResponse(message))
        };
        await using var client = handler.CreateClient();
        await client.InitializeAsync(TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<McpSessionInvalidException>(
            () => client.ListToolsAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(404)]
    [InlineData(503)]
    public async Task Failure_WithoutAssignedSession_KeepsGenericHttpFailure(int status)
    {
        var handler = new McpHttpTestHandler
        {
            Respond = (_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))
        };
        await using var client = handler.CreateClient();

        var exception = await Assert.ThrowsAnyAsync<HttpRequestException>(
            () => client.InitializeAsync(TestContext.Current.CancellationToken));

        Assert.IsType<HttpRequestException>(exception);
    }
}
