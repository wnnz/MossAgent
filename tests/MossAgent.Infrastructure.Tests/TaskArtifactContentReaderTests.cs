using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class TaskArtifactContentReaderTests
{
    [Fact]
    public async Task Read_RejectsJunctionEscapeWithoutReadingForeignContent()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.PreviewTests", Guid.NewGuid().ToString("N"))).FullName;
        string? link = null;
        try
        {
            var paths = new TaskArtifactPaths(new AppDataPaths(root));
            var task = Guid.NewGuid();
            var directory = Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName;
            var outside = Directory.CreateDirectory(Path.Combine(root, "outside")).FullName;
            await File.WriteAllTextAsync(Path.Combine(outside, "secret.txt"), "secret", TestContext.Current.CancellationToken);
            link = Path.Combine(directory, "escape");
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
            foreach (var argument in new[] { "/c", "mklink", "/J", link, outside }) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            await process.WaitForExitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
            var artifact = new ToolArtifact("secret.txt", Path.Combine(link, "secret.txt"), "text/plain", 6, "hash");
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new TaskArtifactContentReader(paths).ReadAsync(task, artifact, TestContext.Current.CancellationToken));
        }
        finally
        {
            if (link is not null && Directory.Exists(link)) Directory.Delete(link);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Read_VerifiesTaskIdentityLengthHashAndFormat()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.PreviewTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var paths = new TaskArtifactPaths(new AppDataPaths(root));
            var task = Guid.NewGuid();
            var directory = Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName;
            var bytes = Encoding.UTF8.GetBytes("preview text");
            var path = Path.Combine(directory, "test.snapshot");
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            var artifact = new ToolArtifact("test.txt", path, "text/plain", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            var reader = new TaskArtifactContentReader(paths);
            Assert.Equal(bytes, await reader.ReadAsync(task, artifact, TestContext.Current.CancellationToken));
            Assert.Equal(bytes, await reader.ReadAsync(task, artifact with { MediaType = "text/x-diff" }, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => reader.ReadAsync(Guid.NewGuid(), artifact, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(task, artifact with { Length = 1 }, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(task, artifact with { Sha256 = new string('0', 64) }, TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(task, artifact with { MediaType = "image/png" }, TestContext.Current.CancellationToken));
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Read_RejectsChangedSnapshotAndCancelledRequest()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.PreviewTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var paths = new TaskArtifactPaths(new AppDataPaths(root));
            var task = Guid.NewGuid();
            var path = Path.Combine(Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName, "changed.snapshot");
            var bytes = Encoding.UTF8.GetBytes("before");
            var artifact = new ToolArtifact("test.txt", path, "text/plain", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
            await File.WriteAllTextAsync(path, "after!", TestContext.Current.CancellationToken);
            var reader = new TaskArtifactContentReader(paths);
            await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(task, artifact, TestContext.Current.CancellationToken));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reader.ReadAsync(task, artifact, cancellation.Token));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
