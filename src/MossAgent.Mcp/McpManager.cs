using System.Collections.Concurrent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Mcp;

public sealed class McpManager(
    IMcpConfigurationRepository configurations,
    McpHttpClientFactory httpClients,
    IMutableToolCatalog tools) : IMcpManager
{
    private readonly ConcurrentDictionary<Guid, IMcpClient> _clients = [];
    private readonly Dictionary<string, string> _errors = [];
    private readonly SemaphoreSlim _reloadGate = new(1, 1);

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
        await _reloadGate.WaitAsync(cancellationToken);
        try
        {
            await DisposeClientsAsync();
            tools.RemoveByPrefix("mcp.");
            lock (_errors)
            {
                _errors.Clear();
            }

            var profiles = await configurations.GetAllAsync(cancellationToken);
            foreach (var profile in profiles.Where(static profile => profile.IsEnabled))
            {
                await LoadServerAsync(profile, cancellationToken);
            }
        }
        finally
        {
            _reloadGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeClientsAsync();
        _reloadGate.Dispose();
    }

    private async Task LoadServerAsync(
        McpServerProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = await CreateClientAsync(profile, cancellationToken);
            await client.InitializeAsync(cancellationToken);
            var definitions = await client.ListToolsAsync(cancellationToken);
            _clients[profile.Id] = client;
            foreach (var definition in definitions)
            {
                var name = $"mcp.{Normalize(profile.Name)}.{Normalize(definition.Name)}";
                tools.RegisterOrReplace(new McpToolAdapter(name, definition.Name, definition, client));
            }
        }
        catch (Exception exception)
        {
            lock (_errors)
            {
                _errors[profile.Name] = exception.Message;
            }
        }
    }

    private async Task<IMcpClient> CreateClientAsync(
        McpServerProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.Transport == McpTransportKind.Stdio)
        {
            return new StdioMcpClient(profile);
        }

        var client = await httpClients.CreateAsync(profile, cancellationToken);
        return new StreamableHttpMcpClient(profile, client);
    }

    private async Task DisposeClientsAsync()
    {
        foreach (var client in _clients.Values)
        {
            await client.DisposeAsync();
        }

        _clients.Clear();
    }

    private static string Normalize(string value)
    {
        var characters = value.Select(character =>
            char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_');
        return new string(characters.ToArray()).Trim('_').ToLowerInvariant();
    }
}
