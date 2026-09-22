namespace CodingAgent.Domain.Models;

/// <summary>工具执行结果。</summary>
public class ToolResult
{
    public ToolResult() { }

    public ToolResult(bool success, string output)
    {
        Success = success;
        Output = output;
    }

    public bool Success { get; set; }
    public string Output { get; set; } = string.Empty;

    public static ToolResult Ok(string output) => new(true, output);
    public static ToolResult Fail(string output) => new(false, output);
}
