using System.Net.Http.Headers;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.Providers;

public sealed class ModelDiscoveryService(IProviderHttpClientFactory clients)
    : IModelDiscoveryService
{
    public async Task<IReadOnlyList<DiscoveredModel>> DiscoverAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        using var client = await clients.CreateAsync(provider, cancellationToken);
        using var request = CreateRequest(provider);
        using var response = await client.SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"获取模型失败：HTTP {(int)response.StatusCode} {Sanitize(content, provider.ApiKey)}");
        }

        using var document = JsonDocument.Parse(content);
        return provider.Protocol switch
        {
            ProviderProtocol.GeminiGenerateContent => ReadGemini(document.RootElement),
            _ => ReadStandard(document.RootElement)
        };
    }

    private static HttpRequestMessage CreateRequest(AiProvider provider)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "models");

        switch (provider.Protocol)
        {
            case ProviderProtocol.AnthropicMessages:
                request.Headers.TryAddWithoutValidation("x-api-key", provider.ApiKey);
                request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
                break;
            case ProviderProtocol.GeminiGenerateContent:
                request.Headers.TryAddWithoutValidation("x-goog-api-key", provider.ApiKey);
                break;
            default:
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", provider.ApiKey);
                break;
        }

        return request;
    }

    private static IReadOnlyList<DiscoveredModel> ReadStandard(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return data.EnumerateArray()
            .Select(ReadStandardItem)
            .Where(static item => item is not null)
            .Cast<DiscoveredModel>()
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DiscoveredModel? ReadStandardItem(JsonElement item)
    {
        if (!item.TryGetProperty("id", out var idProperty))
        {
            return null;
        }

        var id = idProperty.GetString();
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var display = item.TryGetProperty("display_name", out var displayProperty)
            ? displayProperty.GetString()
            : id;
        return new DiscoveredModel(id, string.IsNullOrWhiteSpace(display) ? id : display);
    }

    private static IReadOnlyList<DiscoveredModel> ReadGemini(JsonElement root)
    {
        if (!root.TryGetProperty("models", out var models) || models.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return models.EnumerateArray()
            .Select(ReadGeminiItem)
            .Where(static item => item is not null)
            .Cast<DiscoveredModel>()
            .OrderBy(static item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static DiscoveredModel? ReadGeminiItem(JsonElement item)
    {
        var name = item.TryGetProperty("name", out var nameProperty)
            ? nameProperty.GetString()
            : null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var id = name.StartsWith("models/", StringComparison.Ordinal) ? name[7..] : name;
        var display = item.TryGetProperty("displayName", out var displayProperty)
            ? displayProperty.GetString()
            : id;
        return new DiscoveredModel(id, string.IsNullOrWhiteSpace(display) ? id : display);
    }

    private static string Sanitize(string content, string apiKey)
    {
        var singleLine = content.Replace(apiKey, "***", StringComparison.Ordinal)
            .Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= 300 ? singleLine : singleLine[..300];
    }
}
