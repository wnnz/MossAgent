using System.Security.Cryptography;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class DiffArtifactContentReaderTests
{
    [Fact]
    public async Task DiffMediaType_DoesNotAllowBinaryOrMalformedUtf8Content()
    {
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "MossAgent.PreviewTests", Guid.NewGuid().ToString("N"))).FullName;
        try
        {
            var paths = new TaskArtifactPaths(new AppDataPaths(root));
            var task = Guid.NewGuid();
            var path = Path.Combine(Directory.CreateDirectory(paths.GetTaskDirectory(task)).FullName, "invalid.diff");
            var reader = new TaskArtifactContentReader(paths);
            foreach (var bytes in new[] { new byte[] { 0, 65 }, new byte[] { 255 } })
            {
                await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
                var artifact = new ToolArtifact("invalid.diff", path, "text/x-diff", bytes.Length, Convert.ToHexString(SHA256.HashData(bytes)));
                if (bytes[0] == 0)
                    await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadAsync(task, artifact, TestContext.Current.CancellationToken));
                else
                    await Assert.ThrowsAsync<System.Text.DecoderFallbackException>(() => reader.ReadAsync(task, artifact, TestContext.Current.CancellationToken));
            }
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
