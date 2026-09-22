using System.Text.Json;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Http;

namespace CodingAgent.Infrastructure.Llm;

/// <summary>拉取提供商远端模型列表（GET /v1/models）。</summary>
public class ModelCatalog(ProxyHttpClientFactory proxyFactory) : IModelCatalog
{
    public async Task<List<string>> FetchModelIdsAsync(Provider provider, CancellationToken ct = default)
    {
        using var proxy = proxyFactory.CreateClient(null);
        using var request = new HttpRequestMessage(HttpMethod.Get, OpenAiCompatibleClient.CombineUrl(provider.BaseUrl, "/v1/models"));
        if (provider.Type == Domain.Enums.ProviderType.Anthropic)
        {
            request.Headers.Add("x-api-key", provider.ApiKey);
            request.Headers.Add("anthropic-version", "2023-06-01");
        }
        else if (!string.IsNullOrEmpty(provider.ApiKey))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", provider.ApiKey);
        }

        using var response = await proxy.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ids = new List<string>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            ids.AddRange(data.EnumerateArray()
                .Where(e => e.TryGetProperty("id", out _) && e.GetProperty("id").ValueKind == JsonValueKind.String)
                .Select(e => e.GetProperty("id").GetString()!));
        }
        return ids.Distinct().ToList();
    }
}
