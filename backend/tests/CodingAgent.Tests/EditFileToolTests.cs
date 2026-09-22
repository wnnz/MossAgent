using CodingAgent.Domain.Models;
using CodingAgent.Infrastructure.Tools;
using Xunit;

namespace CodingAgent.Tests;

public class EditFileToolTests : IDisposable
{
    private readonly string _root;
    private readonly EditFileTool _tool = new();

    public EditFileToolTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "codingagent-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "a.txt"), "hello world hello");
    }

    private ToolContext Context() => new() { SessionId = Guid.NewGuid(), WorkspacePath = _root };

    [Fact]
    public async Task ReplaceFirst_WhenSingleOccurrence()
    {
        var result = await _tool.ExecuteAsync("""{"path":"a.txt","old_string":"world","new_string":"there"}""", Context());
        Assert.True(result.Success);
        Assert.Equal("hello there hello", await File.ReadAllTextAsync(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public async Task ReplaceAll_ReplacesAllOccurrences()
    {
        var result = await _tool.ExecuteAsync("""{"path":"a.txt","old_string":"hello","new_string":"hi","replace_all":true}""", Context());
        Assert.True(result.Success);
        Assert.Equal("hi world hi", await File.ReadAllTextAsync(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public async Task MultipleOccurrencesWithoutReplaceAll_Fails()
    {
        var result = await _tool.ExecuteAsync("""{"path":"a.txt","old_string":"hello","new_string":"hi"}""", Context());
        Assert.False(result.Success);
        Assert.Contains("2 次", result.Output);
        Assert.Equal("hello world hello", await File.ReadAllTextAsync(Path.Combine(_root, "a.txt")));
    }

    [Fact]
    public async Task NotFound_Fails()
    {
        var result = await _tool.ExecuteAsync("""{"path":"a.txt","old_string":"missing","new_string":"x"}""", Context());
        Assert.False(result.Success);
        Assert.Contains("未找到", result.Output);
    }

    [Fact]
    public async Task FileNotFound_Fails()
    {
        var result = await _tool.ExecuteAsync("""{"path":"missing.txt","old_string":"a","new_string":"b"}""", Context());
        Assert.False(result.Success);
        Assert.Contains("不存在", result.Output);
    }

    [Fact]
    public async Task EmptyOldString_Fails()
    {
        var result = await _tool.ExecuteAsync("""{"path":"a.txt","old_string":"","new_string":"b"}""", Context());
        Assert.False(result.Success);
    }

    [Fact]
    public async Task PathTraversal_Fails()
    {
        var result = await _tool.ExecuteAsync("""{"path":"../outside.txt","old_string":"a","new_string":"b"}""", Context());
        Assert.False(result.Success);
        Assert.Contains("超出工作区", result.Output);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, true);
        }
        catch (IOException) { }
    }
}
