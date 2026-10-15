using System.Collections.Concurrent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Processes;

namespace MossAgent.Mcp;

public sealed class McpManager(
    IMcpConfigurationRepository configurations,
    McpHttpClientFactory httpClients,
    IMutableToolCatalog tools,
    ITaskProcessLauncher processes,
    TimeSpan? initializationTimeout = null,
    TimeProvider? timeProvider = null) : IMcpManager
{
    private readonly ConcurrentDictionary<Guid, IMcpClient> _clients = [];
    private readonly Dictionary<string, string> _errors = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly McpReloadLifetime _lifetime = new();
    private readonly TimeSpan _initializationTimeout = initializationTimeout ?? TimeSpan.FromSeconds(15);
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public IReadOnlyDictionary<string, string> Errors
    {
        get
        {
            lock (_errors)
            {
                return new Dictionary<string, string>(_errors);
            }
        }
    }

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        using var operation = _lifetime.Enter();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, operation.Token);
        try
        {
            await _reloadGate.WaitAsync(linked.Token).ConfigureAwait(false);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                await DisposeClientsAsync().ConfigureAwait(false);
                tools.RemoveByPrefix("mcp.");
                lock (_errors) { _errors.Clear(); }

                var profiles = await configurations.GetAllAsync(linked.Token).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                foreach (var profile in profiles.Where(static profile => profile.IsEnabled))
                    await LoadServerAsync(profile, linked.Token).ConfigureAwait(false);
            }
            finally { _reloadGate.Release(); }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _lifetime.StopAsync(CleanupAsync);

    private async Task CleanupAsync()
    {
        try { await DisposeClientsAsync().ConfigureAwait(false); }
        finally
        {
            tools.RemoveByPrefix("mcp.");
            _reloadGate.Dispose();
        }
    }

    private async Task LoadServerAsync(
        McpServerProfile profile,
        CancellationToken cancellationToken)
    {
        IMcpClient? client = null;
        using var deadline = new CancellationTokenSource(_initializationTimeout, _timeProvider);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            client = await CreateClientAsync(profile, timeout.Token).ConfigureAwait(false);
            await client.InitializeAsync(timeout.Token).ConfigureAwait(false);
            var definitions = await client.ListToolsAsync(timeout.Token).ConfigureAwait(false);
            var registrations = McpToolRegistration.Create(profile, definitions, client, timeout.Token);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var registration in registrations) tools.RegisterOrReplace(registration);

            _clients[profile.Id] = client;
            client = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            RecordError(profile.Name, "连接或工具发现超时，请检查 MCP 服务状态。");
        }
        catch (Exception exception)
        {
            RecordError(profile.Name, DescribeError(exception));
        }
        finally
        {
            if (client is not null)
            {
                try
                {
                    await client.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            }
        }
    }

    private void RecordError(string name, string message)
    {
        lock (_errors)
        {
            _errors[name] = message;
        }
    }

    private static string DescribeError(Exception exception) => exception switch
    {
        HttpRequestException => "连接失败，请检查 MCP 服务地址、网络和代理设置。",
        InvalidDataException => "服务返回的数据或工具身份无效，请检查 MCP 服务。",
        IOException => "连接已断开，请检查 MCP 服务进程或网络。",
        _ => "加载失败，请检查 MCP 服务配置和运行状态。"
    };

    private async Task<IMcpClient> CreateClientAsync(
        McpServerProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.Transport == McpTransportKind.Stdio)
        {
            return new StdioMcpClient(profile, processes);
        }

        var client = await httpClients.CreateAsync(profile, cancellationToken).ConfigureAwait(false);
        return new StreamableHttpMcpClient(profile, client);
    }

    private async Task DisposeClientsAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync().ConfigureAwait(false);
        }

        _clients.Clear();
    }

}
