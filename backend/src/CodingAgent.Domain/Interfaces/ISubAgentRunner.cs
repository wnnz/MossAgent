using CodingAgent.Domain.Entities;

namespace CodingAgent.Domain.Interfaces;

/// <summary>子代理执行器（task 工具调用入口）。</summary>
public interface ISubAgentRunner
{
    Task<string> RunAsync(SubAgent subAgent, string input, CancellationToken ct = default);
}
