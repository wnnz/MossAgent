using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileMutationToolTests
{
    [Fact]
    public async Task CopyDirectory_CopiesFilesWithoutLeavingAuthorizedRoot()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var source = Directory.CreateDirectory(Path.Combine(root, "source")).FullName;
            await File.WriteAllTextAsync(Path.Combine(source, "hello.txt"), "hello", cancellationToken);
            var destination = Path.Combine(root, "copy");
            var tool = new PathCopyTool(new AuthorizedPathResolver());

            var result = await tool.ExecuteAsync(
                Request(tool, new { source, destination }), Context(root), cancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Equal(
                "hello",
                await File.ReadAllTextAsync(Path.Combine(destination, "hello.txt"), cancellationToken));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task MoveAndDeleteFile_ApplyRequestedMutations()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var source = Path.Combine(root, "before.txt");
            var destination = Path.Combine(root, "nested", "after.txt");
            await File.WriteAllTextAsync(source, "content", cancellationToken);
            var resolver = new AuthorizedPathResolver();
            var move = new PathMoveTool(resolver);
            var delete = new PathDeleteTool(resolver);

            await move.ExecuteAsync(
                Request(move, new { source, destination }), Context(root), cancellationToken);
            var result = await delete.ExecuteAsync(
                Request(delete, new { path = destination }), Context(root), cancellationToken);

            Assert.True(result.IsSuccess);
            Assert.False(File.Exists(source));
            Assert.False(File.Exists(destination));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task DeletePath_RejectsAuthorizedRoot()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var tool = new PathDeleteTool(new AuthorizedPathResolver());
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => tool.ExecuteAsync(
                Request(tool, new { path = root, recursive = true }),
                Context(root),
                cancellationToken));
            Assert.True(Directory.Exists(root));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task FindFiles_FiltersByPattern()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(root, "included.cs"), "class Included;", cancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(root, "excluded.txt"), "text", cancellationToken);
            var tool = new FileFindTool(new AuthorizedPathResolver());

            var result = await tool.ExecuteAsync(
                Request(tool, new { path = root, pattern = "*.cs" }),
                Context(root),
                cancellationToken);

            Assert.Contains("included.cs", result.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("excluded.txt", result.Content, StringComparison.Ordinal);
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    [Fact]
    public async Task WriteFile_RejectsConcurrentExternalChange()
    {
        var root = CreateRoot();
        var cancellationToken = TestContext.Current.CancellationToken;
        try
        {
            var path = Path.Combine(root, "concurrent.txt");
            await File.WriteAllTextAsync(path, "original", cancellationToken);
            var originalHash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes("original")));
            await File.WriteAllTextAsync(path, "external change", cancellationToken);
            var tool = new FileWriteTool(new AuthorizedPathResolver());

            var action = () => tool.ExecuteAsync(
                Request(tool, new { path, content = "agent change", expectedSha256 = originalHash }),
                Context(root),
                cancellationToken);

            await Assert.ThrowsAsync<IOException>(action);
            Assert.Equal("external change", await File.ReadAllTextAsync(path, cancellationToken));
        }
        finally
        {
            DeleteRoot(root);
        }
    }

    private static ToolRequest Request(IAgentTool tool, object arguments) =>
        new(Guid.NewGuid().ToString("N"), tool.Descriptor.Name, JsonSerializer.SerializeToElement(arguments));

    private static ToolExecutionContext Context(string root) =>
        new(Guid.NewGuid(), root, [root], ApprovalPolicy.FullAccess);

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void DeleteRoot(string root)
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
