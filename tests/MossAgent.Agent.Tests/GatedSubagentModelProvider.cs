using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

internal sealed class GatedSubagentModelProvider : IModelProvider
{
    private int _parentTurns;
    private int _activeChildren;
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;
    public TaskCompletionSource Continue { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ChildStopped { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool ChildCompleted { get; private set; }
    public bool ChildCancellationRequested { get; private set; }
    public int ParentTurns => _parentTurns;
    public int ActiveChildren => Volatile.Read(ref _activeChildren);

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Model.ModelId == "child")
        {
            Interlocked.Increment(ref _activeChildren);
            try
            {
                yield return new TextDelta("正在");
                await Continue.Task.WaitAsync(cancellationToken);
                yield return new TextDelta("审查");
                ChildCompleted = true;
            }
            finally
            {
                ChildCancellationRequested = cancellationToken.IsCancellationRequested;
                Interlocked.Decrement(ref _activeChildren);
                ChildStopped.TrySetResult();
            }
            yield break;
        }

        Interlocked.Increment(ref _parentTurns);
        if (!request.Messages.Any(message => message.Role == ModelRole.Tool))
            yield return new ToolCallCompleted("parent-1", "subagent.run",
                JsonSerializer.SerializeToElement(new { agent = "review", task = "检查改动" }));
        else
            yield return new TextDelta("汇总完成");
    }
}
