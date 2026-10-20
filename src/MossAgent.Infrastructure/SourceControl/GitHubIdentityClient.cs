using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

public sealed class GitHubIdentityClient(ISourceAccountHttpClientFactory clients) : ISourceAccountIdentityClient
{
    private const int MaximumBodyBytes = 64 * 1024;

    public async Task<SourceAccountIdentity> GetIdentityAsync(SourceAccount account, CancellationToken cancellationToken)
    {
        if (!account.IsEnabled) throw new SourceConnectionException(SourceConnectionFailure.AccountUnavailable);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        operation.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = await clients.CreateAsync(account, operation.Token);
            using var request = CreateRequest(account);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, operation.Token);
            ValidateStatus(response);
            var body = await ReadBodyAsync(response.Content, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            return ParseIdentity(body, account.AccessToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SourceConnectionException(SourceConnectionFailure.TimedOut);
        }
        catch (OperationCanceledException) { throw; }
        catch (SourceConnectionException) { throw; }
        catch (JsonException) { throw new SourceConnectionException(SourceConnectionFailure.InvalidResponse); }
        catch (Exception) { throw new SourceConnectionException(SourceConnectionFailure.TransportFailure); }
    }

    private static HttpRequestMessage CreateRequest(SourceAccount account)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri(account.ApiEndpoint, "user"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd("MossAgent/1.0");
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return request;
    }

    private static void ValidateStatus(HttpResponseMessage response)
    {
        if (response.StatusCode == HttpStatusCode.OK) return;
        var failure = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => SourceConnectionFailure.InvalidCredential,
            HttpStatusCode.Forbidden when response.Headers.Contains("Retry-After")
                || (response.Headers.TryGetValues("X-RateLimit-Remaining", out var remaining)
                    && remaining.Contains("0")) => SourceConnectionFailure.RateLimited,
            HttpStatusCode.Forbidden => SourceConnectionFailure.Forbidden,
            HttpStatusCode.TooManyRequests => SourceConnectionFailure.RateLimited,
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => SourceConnectionFailure.RedirectDenied,
            _ => SourceConnectionFailure.ServiceUnavailable
        };
        throw new SourceConnectionException(failure);
    }

    private static async Task<byte[]> ReadBodyAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumBodyBytes)
            throw new SourceConnectionException(SourceConnectionFailure.InvalidResponse);
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[4096];
        while (true)
        {
            var count = await stream.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > MaximumBodyBytes)
                throw new SourceConnectionException(SourceConnectionFailure.InvalidResponse);
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }

    private static SourceAccountIdentity ParseIdentity(byte[] body, string accessToken)
    {
        using var document = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("id", out var id)
            || id.ValueKind != JsonValueKind.Number || !id.TryGetInt64(out var userId) || userId <= 0
            || !root.TryGetProperty("login", out var loginValue) || loginValue.ValueKind != JsonValueKind.String)
            throw new SourceConnectionException(SourceConnectionFailure.InvalidResponse);
        var login = loginValue.GetString()!;
        if (login.Length is < 1 or > 100 || login.Contains(accessToken, StringComparison.Ordinal)
            || login.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_' or '[' or ']')))
            throw new SourceConnectionException(SourceConnectionFailure.InvalidResponse);
        return new SourceAccountIdentity(userId, login);
    }
}
