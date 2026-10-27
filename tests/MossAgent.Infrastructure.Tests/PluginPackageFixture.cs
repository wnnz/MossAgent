using System.IO.Compression;

namespace MossAgent.Infrastructure.Tests;

internal sealed class PluginPackageFixture : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "MossAgent.PluginPackage", Guid.NewGuid().ToString("N"));

    public string PackagePath => Path.Combine(Root, "package.mossplugin");

    public CancellationToken Token => Xunit.TestContext.Current.CancellationToken;

    public static PluginTestEntry Manifest(string json = """{"name":"demo-plugin","version":"1.2.3","description":"demo package"}""") =>
        PluginTestEntry.Text("plugin.json", json);

    public string WritePackage(params PluginTestEntry[] entries)
    {
        System.IO.Directory.CreateDirectory(Root);
        using (var file = File.Create(PackagePath))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Create))
        {
            foreach (var item in entries)
            {
                var entry = archive.CreateEntry(item.Name);
                entry.ExternalAttributes = item.ExternalAttributes;
                if (item.Content is not { } bytes) continue;
                using var content = entry.Open();
                content.Write(bytes);
            }
        }
        return PackagePath;
    }

    public string WriteFile(string name, byte[] content)
    {
        System.IO.Directory.CreateDirectory(Root);
        var path = Path.Combine(Root, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    public void Dispose()
    {
        if (System.IO.Directory.Exists(Root)) System.IO.Directory.Delete(Root, true);
    }
}
