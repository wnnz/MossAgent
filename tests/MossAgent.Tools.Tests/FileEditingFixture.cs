using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class FileEditingFixture : IDisposable
{
    public string Root { get; } = Directory.CreateDirectory(System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"))).FullName;
    public string Path => System.IO.Path.Combine(Root, "edit.txt");
    public ToolExecutionContext Context { get; }
    public AuthorizedPathResolver Paths { get; } = new();
    public AtomicTextFileWriter Writer { get; }
    public FileReadTool Read { get; }
    public FileWriteTool Write { get; }
    public TextReplaceTool Replace { get; }
    public CancellationToken Token => TestContext.Current.CancellationToken;

    public FileEditingFixture()
    {
        Context = new ToolExecutionContext(Guid.NewGuid(), Root, [Root], ApprovalPolicy.FullAccess);
        Writer = new AtomicTextFileWriter(Paths);
        Read = new FileReadTool(Paths);
        Write = new FileWriteTool(Paths, Writer);
        Replace = new TextReplaceTool(Paths, Writer);
    }

    public Task SeedAsync(string content, Encoding? encoding = null) =>
        File.WriteAllTextAsync(Path, content, encoding ?? new UTF8Encoding(false), Token);

    public Task<string> ContentsAsync() => File.ReadAllTextAsync(Path, Token);

    public Task<FileSnapshot> SnapshotAsync() => FileSnapshotReader.ReadAsync(Path, Token);

    public Task<ToolResult> ExecuteAsync(IAgentTool tool, object arguments, ToolExecutionContext? context = null)
    {
        var executor = new ToolExecutor(new ToolRegistry([tool]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)));
        return executor.ExecuteAsync(Request(tool, arguments), context ?? Context, Token);
    }

    public static ToolRequest Request(IAgentTool tool, object arguments) =>
        new(Guid.NewGuid().ToString("N"), tool.Descriptor.Name, JsonSerializer.SerializeToElement(arguments));

    public void AssertNoTemporaryFiles() =>
        Assert.Empty(Directory.EnumerateFiles(Root, ".mossagent-*.tmp", SearchOption.AllDirectories));

    public void Dispose() => Directory.Delete(Root, recursive: true);
}
