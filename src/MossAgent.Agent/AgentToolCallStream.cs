using System.Runtime.CompilerServices;
using System.Threading.Channels;
using MossAgent.Application.Agent;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal static class AgentToolCallStream
{
    public static async IAsyncEnumerable<AgentEvent> ExecuteAsync(
        IToolExecutor executor,
        ToolRequest request,
        ToolExecutionContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return new AgentToolProgressEvent(request.ToolName, request.CallId, ToolExecutionPhase.Validating);
        cancellationToken.ThrowIfCancellationRequested();
        using var executionCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var progress = Channel.CreateBounded<AgentEvent>(new BoundedChannelOptions(256)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.DropOldest
        });
        var acceptingProgress = 1;
        var executionContext = context with
        {
            Progress = notification =>
            {
                if (Volatile.Read(ref acceptingProgress) == 0 || notification.TaskId != context.TaskId || notification.CallId != request.CallId
                    || notification.ToolName != request.ToolName || !Enum.IsDefined(notification.Phase)) return;
                if (notification.Phase != ToolExecutionPhase.Validating)
                    progress.Writer.TryWrite(new AgentToolProgressEvent(notification.ToolName, notification.CallId, notification.Phase));
                try { context.Progress?.Invoke(notification); }
                catch (Exception) { }
            },
            SubagentProgress = request.ToolName.Equals(SubagentTool.ToolName, StringComparison.OrdinalIgnoreCase)
                ? message => progress.Writer.TryWrite(new AgentSubagentProgressEvent(request.CallId, message))
                : context.SubagentProgress
        };
        async Task<ToolResult> ExecuteWithProgressAsync()
        {
            try { return await executor.ExecuteAsync(request, executionContext, executionCancellation.Token); }
            finally
            {
                Volatile.Write(ref acceptingProgress, 0);
                progress.Writer.TryComplete();
            }
        }
        var execution = Task.Run(ExecuteWithProgressAsync, CancellationToken.None);
        try
        {
            await foreach (var item in progress.Reader.ReadAllAsync(CancellationToken.None)) yield return item;
            yield return new AgentToolEvent(request.ToolName, request.CallId, await execution);
        }
        finally
        {
            if (!execution.IsCompleted) await executionCancellation.CancelAsync();
            await execution.ConfigureAwait(false);
        }
    }
}
