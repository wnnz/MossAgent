using System.Security.Cryptography;
using System.Text;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Infrastructure.Tests;

internal sealed class ArtifactReadHandleFixture : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MossAgent.ArtifactRead", Guid.NewGuid().ToString("N"));
    public Guid TaskId { get; } = Guid.NewGuid();
    public TaskArtifactPaths Paths { get; }
    public string Directory { get; }
    public string Path { get; }
    public ToolArtifact Artifact { get; }
    public byte[] Bytes { get; } = Encoding.UTF8.GetBytes("artifact content");
    public CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    public ArtifactReadHandleFixture()
    {
        Paths = new(new AppDataPaths(Root));
        Directory = System.IO.Directory.CreateDirectory(Paths.GetTaskDirectory(TaskId)).FullName;
        Path = System.IO.Path.Combine(Directory, "saved.snapshot");
        File.WriteAllBytes(Path, Bytes);
        Artifact = new("saved.txt", Path, "text/plain", Bytes.Length, Convert.ToHexString(SHA256.HashData(Bytes)));
    }

    public void Dispose() => System.IO.Directory.Delete(Root, true);
}
