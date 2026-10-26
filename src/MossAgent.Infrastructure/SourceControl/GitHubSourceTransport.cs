using System.Net;
using System.Net.Http.Headers;
using MossAgent.Application.SourceControl;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.SourceControl;

internal sealed class GitHubSourceTransport(ISourceAccountHttpClientFactory clients)
{
    public async Task<byte[]> GetAsync(SourceAccount account, string relativePath, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = await clients.CreateAsync(account, deadline.Token);
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(account.ApiEndpoint, relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("MossAgent/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            ValidateStatus(response);
            var body = await ReadAsync(response.Content, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            return body;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new SourceReviewException("source_timeout"); }
        catch (OperationCanceledException) { throw; }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_transport_failed"); }
    }

    // Writes share the same headers, deadline and response cap as reads; the caller
    // serializes the JSON body so no string interpolation can corrupt the payload.
    public async Task<byte[]> PostAsync(SourceAccount account, string relativePath, byte[] jsonBody,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = await clients.CreateAsync(account, deadline.Token);
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(account.ApiEndpoint, relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("MossAgent/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Content = new ByteArrayContent(jsonBody);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            ValidateStatus(response);
            var body = await ReadAsync(response.Content, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            return body;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new SourceReviewException("source_timeout"); }
        catch (OperationCanceledException) { throw; }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_transport_failed"); }
    }

    // PATCH uses the identical headers, deadline, body cap and status mapping as POST;
    // only the HTTP method differs.
    public async Task<byte[]> PatchAsync(SourceAccount account, string relativePath, byte[] jsonBody,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try
        {
            using var client = await clients.CreateAsync(account, deadline.Token);
            using var request = new HttpRequestMessage(HttpMethod.Patch, new Uri(account.ApiEndpoint, relativePath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", account.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            request.Headers.UserAgent.ParseAdd("MossAgent/1.0");
            request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
            request.Content = new ByteArrayContent(jsonBody);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token);
            ValidateStatus(response);
            var body = await ReadAsync(response.Content, deadline.Token);
            deadline.Token.ThrowIfCancellationRequested();
            return body;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new SourceReviewException("source_timeout"); }
        catch (OperationCanceledException) { throw; }
        catch (SourceReviewException) { throw; }
        catch (Exception) { throw new SourceReviewException("source_transport_failed"); }
    }

    private static void ValidateStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Created) return;
        throw new SourceReviewException(response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "source_auth_failed",
            HttpStatusCode.Forbidden when response.Headers.Contains("Retry-After")
                || (response.Headers.TryGetValues("X-RateLimit-Remaining", out var values) && values.Contains("0")) => "source_rate_limited",
            HttpStatusCode.Forbidden => "source_forbidden",
            HttpStatusCode.NotFound => "source_not_found",
            HttpStatusCode.Gone => "source_not_found",
            HttpStatusCode.UnprocessableEntity => "source_write_rejected",
            HttpStatusCode.TooManyRequests => "source_rate_limited",
            >= HttpStatusCode.MultipleChoices and < HttpStatusCode.BadRequest => "source_redirect_denied",
            _ => "source_unavailable"
        });
    }

    private static async Task<byte[]> ReadAsync(HttpContent content, CancellationToken cancellationToken)
    {
        const int maximumBytes = 2 * 1024 * 1024;
        if (content.Headers.ContentLength > maximumBytes) throw new SourceReviewException("source_invalid_response");
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var count = await stream.ReadAsync(chunk, cancellationToken);
            if (count == 0) break;
            if (buffer.Length + count > maximumBytes) throw new SourceReviewException("source_invalid_response");
            buffer.Write(chunk, 0, count);
        }
        return buffer.ToArray();
    }
}
