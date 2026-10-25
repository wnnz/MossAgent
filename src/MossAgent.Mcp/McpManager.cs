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
    TimeProvider? timeProvider = null,
    TimeSpan? drainTimeout = null) : IMcpManager
{
    private readonly ConcurrentDictionary<Guid, McpClientHandle> _clients = [];
    private readonly Dictionary<string, string> _errors = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);
    private readonly McpReloadLifetime _lifetime = new();
    private readonly TimeSpan _initializationTimeout = initializationTimeout ?? TimeSpan.FromSeconds(15);
    private readonly TimeSpan _drainTimeout = drainTimeout ?? TimeSpan.FromSeconds(5);
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
                // Tools go unavailable before clients drain so new lookups fail
                // fast and in-flight calls are never retargeted to new clients.
                tools.RemoveByPrefix("mcp.");
                await DisposeClientsAsync().ConfigureAwait(false);
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
        try
        {
            tools.RemoveByPrefix("mcp.");
            await DisposeClientsAsync().ConfigureAwait(false);
        }
        finally
        {
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
            var handle = new McpClientHandle(client);
            await handle.InitializeAsync(timeout.Token).ConfigureAwait(false);
            var definitions = await handle.ListToolsAsync(timeout.Token).ConfigureAwait(false);
            var registrations = McpToolRegistration.Create(profile, definitions, handle, timeout.Token);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var registration in registrations) tools.RegisterOrReplace(registration);

            _clients[profile.Id] = handle;
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
        McpAuthenticationException { ChallengeScheme: { } scheme } authentication =>
            $"服务要求 {scheme} 认证（{(int)authentication.StatusCode}）：请在服务器 headers 中配置有效凭据后重载。",
        McpAuthenticationException authentication =>
            $"认证或权限失败（{(int)authentication.StatusCode}）：凭据缺失或权限不足，请检查 headers/凭据与账号权限。",
        McpSessionInvalidException => "MCP 会话已失效（404）：请重新加载或检查服务端会话支持。",
        McpStdioExitException exit =>
            $"MCP 进程已退出（退出码 {exit.ExitCode?.ToString() ?? "未知"}）：请检查启动命令、工作目录和环境变量。",
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
        var handles = _clients.Values.ToArray();
        _clients.Clear();
        foreach (var handle in handles)
        {
            try
            {
                await handle.DrainAsync(_drainTimeout).ConfigureAwait(false);
            }
            finally
            {
                await handle.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

}
