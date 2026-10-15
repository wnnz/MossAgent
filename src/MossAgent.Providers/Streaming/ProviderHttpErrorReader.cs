using System.Net;
using System.Text;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers.Streaming;

internal static class ProviderHttpErrorReader
{
    public const int MaximumBodyBytes = 64 * 1024;

    public static async Task<ModelFailed> ReadAsync(HttpContent content, HttpStatusCode status,
        AiProvider provider, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (status == HttpStatusCode.ProxyAuthenticationRequired)
            return new ModelFailed("proxy_authentication_failed", $"HTTP 407: {ProviderText.Get("ProxyAuthenticationFailed")}");
        if (content.Headers.ContentLength > MaximumBodyBytes)
            return Failure(status, ProviderText.Get("HttpErrorBodyLimit"));
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        var buffer = new byte[MaximumBodyBytes + 1];
        var total = 0;
        while (total < buffer.Length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(total), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (count == 0) break;
            total += count;
            if (total > MaximumBodyBytes) return Failure(status, ProviderText.Get("HttpErrorBodyLimit"));
        }
        try
        {
            var error = new UTF8Encoding(false, true).GetString(buffer, 0, total);
            return Failure(status, ProviderErrorSanitizer.Sanitize(error, provider));
        }
        catch (DecoderFallbackException)
        {
            return Failure(status, ProviderText.Get("HttpErrorInvalidEncoding"));
        }
    }

    private static ModelFailed Failure(HttpStatusCode status, string message) =>
        new("http_error", $"HTTP {(int)status}: {message}");
}
