using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;
using MossAgent.Tools.BuiltIn.SourceControl;

namespace MossAgent.Tools.BuiltIn.Execution;

internal static class ToolAsyncRequestValidation
{
    public static async Task<ToolResult?> ValidateAsync(IAgentTool tool, ToolRequest request,
        ToolExecutionContext context, CancellationToken cancellationToken, TimeProvider timeProvider)
    {
        if (tool is not IToolRequestAsyncPrevalidator validator) return null;
        using var deadline = new CancellationTokenSource(context.Timeout ?? TimeSpan.FromSeconds(30), timeProvider);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try
        {
            operation.Token.ThrowIfCancellationRequested();
            await validator.ValidateRequestAsync(request, context, operation.Token);
            operation.Token.ThrowIfCancellationRequested();
            return null;
        }
        catch (SourceReviewException exception)
        { return SourceReadFailure.Create(exception.ErrorCode); }
        catch (OperationCanceledException)
        {
            return ToolResult.Failure(ToolExecutionText.Get(cancellationToken.IsCancellationRequested ? "ExecutionCancelled" : "ExecutionTimedOut"),
                cancellationToken.IsCancellationRequested ? "tool_cancelled" : "tool_timeout");
        }
        catch (Exception) { return ToolResult.Failure(ToolExecutionText.Get("SourceReadFailed"), "source_scope_denied"); }
    }
}
