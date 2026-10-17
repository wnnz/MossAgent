using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Tools.BuiltIn.Execution;

namespace MossAgent.App.Tests;

internal sealed class McpSettingsReloadFixture : IDisposable
{
    public McpSettingsReloadFixture(bool initialFailure = false, bool blockReconnect = false)
    {
        Repository.Items = [Server("initial")];
        if (initialFailure) Repository.Reader = _ => Task.FromException<IReadOnlyList<McpServerProfile>>(
            new IOException("synthetic-private-repository-error"));
        if (!blockReconnect) Manager.Release.TrySetResult();
        Model = new McpSettingsViewModel(Repository, Proxies, Manager, new ToolRegistry([]));
    }

    public McpReloadProbeRepository Repository { get; } = new();
    public ProxyReloadProbeRepository Proxies { get; } = new();
    public DeferredStartupMcpManager Manager { get; } = new();
    public McpSettingsViewModel Model { get; }

    public static McpServerProfile Server(string name) => new(
        Guid.NewGuid(), name, McpTransportKind.Stdio, "not-started", "[]", null, "{}", null, "{}", null, false);

    public static ProxyProfile Proxy(string name) => new(
        Guid.NewGuid(), name, ProxyProtocol.Http, "127.0.0.1", 8080, null, null, false, true);

    public void Dispose()
    {
        Model.Dispose();
        Manager.Release.TrySetResult();
    }
}
