using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Providers;

internal static class ProviderErrorSanitizer
{
    private const int MaximumLength = 500;

    public static string Sanitize(string value, AiProvider provider)
    {
        var sanitized = Redact(value, provider.ApiKey);
        foreach (var secret in provider.Headers.Values)
        {
            sanitized = Redact(sanitized, secret);
        }

        sanitized = sanitized.Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal);
        return sanitized.Length <= MaximumLength
            ? sanitized
            : sanitized[..MaximumLength] + "…";
    }

    public static ModelFailed Sanitize(ModelFailed failure, AiProvider provider) =>
        new(Sanitize(failure.Code, provider), Sanitize(failure.Message, provider));

    public static ModelEvent Sanitize(ModelEvent modelEvent, AiProvider provider) =>
        modelEvent is ModelFailed failure ? Sanitize(failure, provider) : modelEvent;

    private static string Redact(string value, string secret) =>
        string.IsNullOrWhiteSpace(secret)
            ? value
            : value.Replace(secret, "***", StringComparison.Ordinal);
}
