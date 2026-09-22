using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Models;

namespace CodingAgent.Domain.Interfaces;

/// <summary>LLM 客户端：按提供商协议流式调用模型。</summary>
public interface ILlmClient
{
    IAsyncEnumerable<LlmStreamEvent> StreamAsync(LlmRequest request, CancellationToken ct = default);
}

public interface IWorkspaceLocator
{
    /// <summary>当前工作区根目录（settings.workspace_path 或默认目录）。</summary>
    string WorkspaceRoot { get; }
}

/// <summary>按提供商配置创建对应协议的客户端（含网络代理）。</summary>
public interface ILlmClientFactory
{
    ILlmClient Create(Provider provider);
}

/// <summary>拉取提供商远端模型列表。</summary>
public interface IModelCatalog
{
    Task<List<string>> FetchModelIdsAsync(Provider provider, CancellationToken ct = default);
}
