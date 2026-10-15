using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class FileChangeSubagentModelProvider(string path, string ending, Action cancel) : IModelProvider
{
    private int _turn;
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;

    public async IAsyncEnumerable<ModelEvent> StreamAsync(ModelRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (++_turn == 1)
        {
            yield return new ToolCallCompleted("child-write", "filesystem.write_file",
                JsonSerializer.SerializeToElement(new { path, content = "committed child content" }));
            yield break;
        }
        if (ending == "cancel") { cancel(); cancellationToken.ThrowIfCancellationRequested(); }
        if (ending == "timeout") await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        yield return ending == "failure" ? new ModelFailed("synthetic", "private-fixture-error") : new TextDelta("完成");
    }
}
