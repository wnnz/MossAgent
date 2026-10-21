using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;
using MossAgent.Domain;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolWorkspaceValidation
{
    public static async Task<ToolResult?> ValidateAsync(ToolExecutionContext context, CancellationToken cancellationToken,
        TimeProvider timeProvider)
    {
        if (context.WorkspaceGuard is null) return null;
        var duration = context.Timeout is { } requested && requested < TimeSpan.FromSeconds(30)
            && requested != Timeout.InfiniteTimeSpan ? requested : TimeSpan.FromSeconds(30);
        using var deadline = new CancellationTokenSource(duration, timeProvider);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            operation.Token.ThrowIfCancellationRequested();
            await context.ValidateWorkspaceAsync(operation.Token).ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            return null;
        }
        catch (WorkspaceAuthorizationException)
        {
            return ToolResult.Failure(ToolExecutionText.Get("WorkspaceAuthorizationChanged"), "workspace_authorization_changed");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return ToolResult.Failure(ToolExecutionText.Get("ExecutionCancelled"), "tool_cancelled");
        }
        catch (Exception failure) when (failure is TimeoutException || failure is OperationCanceledException && deadline.IsCancellationRequested)
        {
            return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout");
        }
        catch (Exception)
        {
            return ToolResult.Failure(ToolExecutionText.Get("WorkspaceAccessDenied"), "workspace_denied");
        }
    }
}
