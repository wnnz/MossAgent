using CodingAgent.Domain.Models;

namespace CodingAgent.Domain.Interfaces;

/// <summary>Agent 可用工具。实现按 DI 注入所需服务，通过 ToolContext 获得会话/工作区信息。</summary>
public interface ITool
{
    string Name { get; }
    string Description { get; }
    /// <summary>参数 JSON Schema（对象）。</summary>
    string ParametersSchemaJson { get; }
    Task<ToolResult> ExecuteAsync(string argumentsJson, ToolContext context, CancellationToken ct = default);
}

public interface IWorkspaceGuard
{
    string WorkspaceRoot { get; }
    /// <summary>校验路径位于工作区内（拒绝路径穿越/绝对路径逃逸），返回规范化绝对路径。</summary>
    string ResolveInsideWorkspace(string path);
    void EnsureInsideWorkspace(string path);
}
