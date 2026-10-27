using System.Text;

namespace MossAgent.Infrastructure.Tests;

internal sealed record PluginTestEntry(string Name, byte[]? Content = null, int ExternalAttributes = 0)
{
    public static PluginTestEntry Text(string name, string content) => new(name, Encoding.UTF8.GetBytes(content));

    public static PluginTestEntry Bytes(string name, int length) => new(name, new byte[length]);

    public static PluginTestEntry Directory(string name) => new(name.EndsWith('/') ? name : name + "/");
}
