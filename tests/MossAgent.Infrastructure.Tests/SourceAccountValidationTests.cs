using System.Text.Json;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SourceAccountValidationTests
{
    [Theory]
    [InlineData("http://api.github.com/")]
    [InlineData("https://user:credential@example.test/api/v3/")]
    [InlineData("https://api.github.com/?access_token=credential")]
    [InlineData("https://api.github.com/#credential")]
    [InlineData("https://github.com/")]
    [InlineData("https://api.github.com:8443/")]
    [InlineData("https://api.github.com/api/v3/")]
    [InlineData("https://example.test/api/v3/../v3/")]
    [InlineData("https://example.test/api/%76%33/")]
    [InlineData("https://example.test/api/v3%2f/")]
    [InlineData("https://example.test/api\\v3/")]
    [InlineData("https://example.test./api/v3/")]
    [InlineData("https://example.test/arbitrary/")]
    [InlineData(" https://api.github.com/")]
    [InlineData("file:///C:/private")]
    public void EndpointRejectsCredentialAndPathAmbiguity(string endpoint) =>
        Assert.Throws<ArgumentException>(() => GitHubApiEndpoint.Parse(endpoint));

    [Theory]
    [InlineData("https://api.github.com", "https://api.github.com/")]
    [InlineData("https://enterprise.example.test/api/v3", "https://enterprise.example.test/api/v3/")]
    [InlineData("https://enterprise.example.test:8443/api/v3/", "https://enterprise.example.test:8443/api/v3/")]
    public void EndpointNormalizesExplicitApiPrefix(string value, string expected) =>
        Assert.Equal(expected, GitHubApiEndpoint.Parse(value).AbsoluteUri);

    [Theory]
    [InlineData("")]
    [InlineData("with space")]
    [InlineData("credential\r\nHeader: injected")]
    [InlineData("credential\tvalue")]
    [InlineData("令牌")]
    public void InvalidTokenNeverAppearsInValidationError(string token)
    {
        var exception = Assert.Throws<ArgumentException>(() => Create(token));
        if (token.Length > 0) Assert.DoesNotContain(token, exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void SerializationAndDisplayExcludeCredential()
    {
        const string secret = "synthetic-fixture-source-credential";
        var account = Create(secret);
        Assert.DoesNotContain(secret, account.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(secret, JsonSerializer.Serialize(account), StringComparison.Ordinal);
        Assert.Equal(secret, account.WithRevision(2).AccessToken);
    }

    internal static SourceAccount Create(string token = "synthetic-source-credential", Guid? proxyId = null) =>
        new(Guid.NewGuid(), "source fixture", new Uri("https://enterprise.example.test/api/v3/"), token, proxyId, true);
}
