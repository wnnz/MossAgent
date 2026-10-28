using System.Runtime.CompilerServices;
using System.Text.Json;
using MossAgent.Agent;
using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.Agent.Tests;

/// <summary>
/// Scripted provider for concurrent-delegation coverage. A parent's first turn delegates its
/// own user prompt to the "review" subagent; later turns finish with a summary. Every child
/// stream parks inside <see cref="BothStarted"/> until the expected number of children are
/// in-flight at once, then echoes its own delegated task — so a run is identifiable by
/// content and a serialized implementation deadlocks against the test timeout instead of
/// passing silently. A task containing <see cref="FailureMarker"/> fails the child model.
/// </summary>
internal sealed class ConcurrentDelegationModelProvider(int expectedChildren) : IModelProvider
{
    public const string FailureMarker = "必须失败";
    private int _startedChildren;
    public ProviderProtocol Protocol => ProviderProtocol.OpenAiResponses;
    public TaskCompletionSource BothStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public int StartedChildren => Volatile.Read(ref _startedChildren);

    public async IAsyncEnumerable<ModelEvent> StreamAsync(
        ModelRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (request.Model.ModelId == "child")
        {
            if (Interlocked.Increment(ref _startedChildren) == expectedChildren)
                BothStarted.TrySetResult();
            await BothStarted.Task.WaitAsync(cancellationToken);
            var task = request.Messages.Last(static message => message.Role == ModelRole.User).Content;
            if (task.Contains(FailureMarker, StringComparison.Ordinal))
                yield return new ModelFailed("provider_error", "synthetic");
            else
                yield return new TextDelta($"已完成：{task}");
            yield break;
        }

        if (!request.Messages.Any(static message => message.Role == ModelRole.Tool))
        {
            var prompt = request.Messages.Last(static message => message.Role == ModelRole.User).Content;
            yield return new ToolCallCompleted("parent-1", SubagentTool.ToolName,
                JsonSerializer.SerializeToElement(new { agent = "review", task = prompt }));
        }
        else
        {
            yield return new TextDelta("汇总完成");
        }
    }
}
