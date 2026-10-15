using System.Text.Json;

namespace MossAgent.App.Tests;

internal sealed class CompatibleStreamFlowServer : IAsyncDisposable
{
    private readonly WorkspaceModelStreamServer _server;

    public CompatibleStreamFlowServer(string path, CompatibleStreamScenario scenario, CancellationToken token)
    {
        _server = new WorkspaceModelStreamServer(
            (request, turn) => CompatibleStreamFlowResponses.Create(request, turn, path, scenario), token);
    }

    public Uri BaseUri => _server.BaseUri;
    public IReadOnlyList<(string Target, JsonElement Body)> Requests => _server.Requests;
    public ValueTask DisposeAsync() => _server.DisposeAsync();
}
