using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

/// <summary>
/// Scripted provider for mid-run authorization revocation. The parent's first turn delegates
/// to "review". The child's first turn calls <c>test.read</c>; its second turn signals
/// <see cref="ChildTurn2Started"/> and parks on the caller token, so a test can revoke the
/// workspace lease while the child model turn is in-flight.
/// </summary>
internal sealed class RevocationGatedModelProvider : IModelProvider
{
    private int _childTurns;
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;
    public TaskCompletionSource ChildTurn2Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int ChildTurns => Volatile.Read(ref _childTurns);

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Model.ModelId == "child")
        {
            if (Interlocked.Increment(ref _childTurns) == 1)
            {
                yield return new ToolCallCompleted("child-1", "test.read",
                    JsonSerializer.SerializeToElement(new { }));
                yield break;
            }
            ChildTurn2Started.TrySetResult();
            await Task.Delay(Timeout.Infinite, cancellationToken);
            yield return new TextDelta("不应到达");
            yield break;
        }

        if (!request.Messages.Any(static message => message.Role == ModelRole.Tool))
        {
            yield return new ToolCallCompleted("parent-1", SubagentTool.ToolName,
                JsonSerializer.SerializeToElement(new { agent = "review", task = "检查改动" }));
        }
        else
        {
            yield return new TextDelta("汇总完成");
        }
    }
}
