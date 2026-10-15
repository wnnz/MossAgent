using System.Net.Http.Headers;
using System.Net;
using System.Text;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Domain;

namespace MossAgent.Providers;

public sealed class ModelDiscoveryService(IProviderHttpClientFactory clients)
    : IModelDiscoveryService
{
    private const int MaximumGeminiModelPages = 16;
    private const int MaximumModelResponseBytes = 2 * 1024 * 1024;

    public async Task<IReadOnlyList<DiscoveredModel>> DiscoverAsync(
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        using var client = await clients.CreateAsync(provider, cancellationToken);
        if (provider.Protocol == ProviderProtocol.GeminiGenerateContent)
        {
            return await DiscoverGeminiAsync(client, provider, cancellationToken);
        }

        using var request = CreateRequest(provider);
        var content = await SendRequestAsync(client, request, provider, cancellationToken);
        using var document = JsonDocument.Parse(content);
        return ReadStandard(document.RootElement);
    }

    private async Task<IReadOnlyList<DiscoveredModel>> DiscoverGeminiAsync(
        HttpClient client,
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        var models = new Dictionary<string, DiscoveredModel>(StringComparer.OrdinalIgnoreCase);
        var seenPageTokens = new HashSet<string>(StringComparer.Ordinal);
        string? pageToken = null;
        for (var page = 0; page < MaximumGeminiModelPages; page++)
        {
            using var request = CreateRequest(provider, pageToken);
            var content = await SendRequestAsync(client, request, provider, cancellationToken);
            using var document = JsonDocument.Parse(content);
            foreach (var model in GeminiModelDiscoveryParser.ReadModels(document.RootElement))
            {
                models.TryAdd(model.Id, model);
            }

            pageToken = GeminiModelDiscoveryParser.ReadNextPageToken(document.RootElement);
            if (pageToken is null)
            {
                return models.Values
                    .OrderBy(static model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }

            if (!seenPageTokens.Add(pageToken))
            {
                throw new InvalidDataException("供应商返回了重复的模型分页标记。");
            }
        }

        throw new InvalidDataException("供应商模型列表超过安全分页上限。");
    }

    private static async Task<string> SendRequestAsync(
        HttpClient client,
        HttpRequestMessage request,
        AiProvider provider,
        CancellationToken cancellationToken)
    {
        HttpStatusCode statusCode;
        string content;
        try
        {
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            statusCode = response.StatusCode;
            content = statusCode == HttpStatusCode.ProxyAuthenticationRequired
                ? ProviderText.Get("ProxyAuthenticationFailed")
                : await ReadBoundedResponseAsync(response.Content, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw CreateSafeRequestException(exception);
        }

        if ((int)statusCode is < 200 or >= 300)
        {
            throw new HttpRequestException(
                $"获取模型失败：HTTP {(int)statusCode} {ProviderErrorSanitizer.Sanitize(content, provider)}",
                null,
                statusCode);
        }

        return content;
    }

    private static async Task<string> ReadBoundedResponseAsync(
        HttpContent content,
        CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaximumModelResponseBytes)
        {
            throw new InvalidDataException("供应商模型响应超过 2 MiB 上限。");
        }

        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var remaining = MaximumModelResponseBytes - (int)output.Length;
            var count = await stream.ReadAsync(
                chunk.AsMemory(0, Math.Min(chunk.Length, remaining + 1)), cancellationToken);
            if (count == 0) break;
            if (count > remaining)
            {
                throw new InvalidDataException("供应商模型响应超过 2 MiB 上限。");
            }

            output.Write(chunk, 0, count);
        }

        output.Position = 0;
        var charset = content.Headers.ContentType?.CharSet?.Trim('"');
        var encoding = string.IsNullOrWhiteSpace(charset)
            ? Encoding.UTF8
            : Encoding.GetEncoding(charset);
        using var reader = new StreamReader(output, encoding, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    private static HttpRequestMessage CreateRequest(AiProvider provider, string? pageToken = null)
    {
        var requestUri = string.IsNullOrWhiteSpace(pageToken)
            ? "models"
            : $"models?pageToken={Uri.EscapeDataString(pageToken)}";
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

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

    private static HttpRequestException CreateSafeRequestException(HttpRequestException exception)
    {
        var message = exception.StatusCode is { } statusCode
            ? $"获取模型失败：HTTP {(int)statusCode}。"
            : "获取模型失败：无法连接到供应商。";
        return new HttpRequestException(message, null, exception.StatusCode);
    }
}
