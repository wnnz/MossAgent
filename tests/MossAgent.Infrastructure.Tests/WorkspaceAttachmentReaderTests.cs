using System.Diagnostics;
using System.Text;
using MossAgent.Domain;
using MossAgent.Infrastructure.Attachments;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class WorkspaceAttachmentReaderTests
{
    [Fact]
    public async Task ReadAsync_ReturnsUtf8TextInsideAuthorizedRoot()
    {
        var root = CreateTestRoot();
        try
        {
            var path = Path.Combine(root, "sample.cs");
            await File.WriteAllTextAsync(
                path, "class Sample { }", new UTF8Encoding(false),
                TestContext.Current.CancellationToken);

            var result = await new WorkspaceAttachmentReader().ReadAsync(
                CreateProject(root), [path], TestContext.Current.CancellationToken);

            var attachment = Assert.Single(result);
            Assert.Equal("class Sample { }", attachment.Content);
            Assert.EndsWith("sample.cs", attachment.DisplayPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsPathOutsideAuthorizedRoots()
    {
        var root = CreateTestRoot();
        var outside = Path.Combine(Path.GetDirectoryName(root)!, $"{Guid.NewGuid():N}.txt");
        try
        {
            await File.WriteAllTextAsync(
                outside, "secret", TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new WorkspaceAttachmentReader().ReadAsync(
                    CreateProject(root), [outside], TestContext.Current.CancellationToken));
        }
        finally
        {
            File.Delete(outside);
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x00, 0x42 }, "二进制")]
    [InlineData(new byte[] { 0xC3, 0x28 }, "UTF-8")]
    public async Task ReadAsync_RejectsUnsupportedContent(byte[] bytes, string message)
    {
        var root = CreateTestRoot();
        try
        {
            var path = Path.Combine(root, "invalid.bin");
            await File.WriteAllBytesAsync(
                path, bytes, TestContext.Current.CancellationToken);

            var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkspaceAttachmentReader().ReadAsync(
                    CreateProject(root), [path], TestContext.Current.CancellationToken));

            Assert.Contains(message, error.Message);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsOversizedFile()
    {
        var root = CreateTestRoot();
        try
        {
            var path = Path.Combine(root, "large.txt");
            await File.WriteAllBytesAsync(
                path, new byte[129 * 1024], TestContext.Current.CancellationToken);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new WorkspaceAttachmentReader().ReadAsync(
                    CreateProject(root), [path], TestContext.Current.CancellationToken));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReadAsync_RejectsJunctionEscape()
    {
        var testRoot = CreateTestRoot();
        var authorized = Directory.CreateDirectory(Path.Combine(testRoot, "authorized")).FullName;
        var outside = Directory.CreateDirectory(Path.Combine(testRoot, "outside")).FullName;
        var link = Path.Combine(authorized, "escape");
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(outside, "secret.txt"), "secret",
                TestContext.Current.CancellationToken);
            CreateJunction(link, outside);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                new WorkspaceAttachmentReader().ReadAsync(
                    CreateProject(authorized), [Path.Combine(link, "secret.txt")],
                    TestContext.Current.CancellationToken));
        }
        finally
        {
            if (Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(testRoot, recursive: true);
        }
    }

    private static string CreateTestRoot() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "MossAgent.AttachmentTests", Guid.NewGuid().ToString("N"))).FullName;

    private static ProjectProfile CreateProject(string root) => new(
        Guid.NewGuid(), "test", root, [root], DateTimeOffset.UtcNow);

    private static void CreateJunction(string link, string target)
    {
        var startInfo = new ProcessStartInfo("cmd.exe")
        {
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("/c");
        startInfo.ArgumentList.Add("mklink");
        startInfo.ArgumentList.Add("/J");
        startInfo.ArgumentList.Add(link);
        startInfo.ArgumentList.Add(target);
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法创建 junction 测试夹具。");
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
