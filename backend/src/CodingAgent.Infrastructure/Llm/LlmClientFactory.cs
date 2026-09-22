using System.Collections.Concurrent;
using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Enums;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Http;

namespace CodingAgent.Infrastructure.Llm;

/// <summary>按提供商协议与代理配置创建 LLM 客户端（HttpClient 按 provider+代理配置缓存，避免句柄泄漏）。</summary>
public class LlmClientFactory(ProxyHttpClientFactory proxyFactory, IProxyRepository proxyRepository) : ILlmClientFactory
{
    private readonly ConcurrentDictionary<string, HttpClient> _httpClients = new();

    public ILlmClient Create(Provider provider)
    {
        var http = _httpClients.GetOrAdd(BuildKey(provider), _ =>
        {
            var proxy = provider.ProxyId is { } proxyId
                ? proxyRepository.GetAsync(proxyId).GetAwaiter().GetResult()
                : null;
            return proxyFactory.CreateClient(proxy);
        });

        return provider.Type switch
        {
            ProviderType.OpenAiCompatible => new OpenAiCompatibleClient(http, provider),
            ProviderType.Anthropic => new AnthropicClient(http, provider),
            _ => throw new NotSupportedException($"不支持的提供商类型: {provider.Type}"),
        };
    }

    private static string BuildKey(Provider provider)
    {
        var proxyPart = provider.ProxyId is { } proxyId ? $"proxy={proxyId}" : "noproxy";
        return $"{provider.Id}|{provider.Type}|{provider.BaseUrl}|{proxyPart}";
    }
}
