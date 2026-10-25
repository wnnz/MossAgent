namespace MossAgent.Application.Agent;

/// <summary>
/// 描述一次失败任务在请求边界上推进到哪个阶段，用于决定显式重试需要的确认级别。
/// </summary>
public enum RunFailureProgress
{
    /// <summary>无法确定请求是否到达提供商，保留未知状态。</summary>
    Unknown = 0,
    /// <summary>请求在进程内前置校验或连接建立阶段失败，未提交到提供商。</summary>
    NotSubmitted = 1,
    /// <summary>响应流或 HTTP 层证明请求已到达提供商，可能已被计费。</summary>
    PossiblySubmitted = 2,
    /// <summary>失败前已有工具结果返回，重试不会撤销已产生的副作用。</summary>
    ToolsExecuted = 3
}
