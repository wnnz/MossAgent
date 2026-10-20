using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitCommitTool(GitCommitStateReader states, GitCommandRunner git) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.commit", ToolExecutionText.Get("GitCommitDescription"),
        """{"type":"object","additionalProperties":false,"required":["message","expectedState"],"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"message":{"type":"string","minLength":1,"maxLength":8192,"pattern":"\\S"},"expectedState":{"type":"string","pattern":"^[0-9A-Fa-f]{64}$"}}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileRead | ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        states.Validate(request, context);
        var message = request.Arguments.GetRequiredString("message");
        if (message.Any(character => char.IsControl(character) && character is not ('\r' or '\n' or '\t')))
            throw new ArgumentException(ToolExecutionText.Get("GitCommitMessageInvalid"));
    }

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await states.CaptureAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (!snapshot.Preview.State.Equals(request.Arguments.GetRequiredString("expectedState"), StringComparison.OrdinalIgnoreCase))
                return ToolResult.Failure(ToolExecutionText.Get("GitCommitStateChanged"), "git_state_changed");
            if (snapshot.Preview.StagedFiles == 0)
                return ToolResult.Failure(ToolExecutionText.Get("GitCommitNothingStaged"), "git_nothing_staged");
            cancellationToken.ThrowIfCancellationRequested();
            return await git.RunAsync(snapshot.Repository.Root,
                snapshot.Repository.Command("commit", "--cleanup=verbatim", "--message=" + request.Arguments.GetRequiredString("message")),
                context, cancellationToken).ConfigureAwait(false);
        }
        catch (GitCommitException failure) { return ToolResult.Failure(failure.Message, failure.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }
}
