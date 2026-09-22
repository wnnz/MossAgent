using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitStatusToolTests
{
    [Fact]
    public async Task NullTerminatedStatus_PreservesUnicodeAndSpaces()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var context = new ToolExecutionContext(
                Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);
            var runner = new GitCommandRunner();
            var initialized = await runner.RunAsync(root, ["init"], context, cancellationToken);
            Assert.True(initialized.IsSuccess, initialized.Summary);
            await File.WriteAllTextAsync(
                Path.Combine(root, "hello world.txt"), "hello", cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(root, "新文件.txt"), "hello", cancellationToken);
            var arguments = JsonDocument.Parse("{\"nullTerminated\":true}").RootElement.Clone();
            var tool = new GitStatusTool(new AuthorizedPathResolver(), runner);

            var result = await tool.ExecuteAsync(
                new ToolRequest("1", "git.status", arguments), context, cancellationToken);

            Assert.True(result.IsSuccess, result.Summary);
            Assert.Contains('\0', result.Content!);
            Assert.Contains("hello world.txt", result.Content!);
            Assert.Contains("新文件.txt", result.Content!);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
