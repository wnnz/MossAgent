using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitPushTool(GitPushStateReader states, GitCommandRunner git) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.push", ToolExecutionText.Get("GitPushDescription"),
        """{"type":"object","additionalProperties":false,"required":["remote","targetBranch","expectedState"],"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"remote":{"type":"string","minLength":1,"maxLength":256},"targetBranch":{"type":"string","minLength":1,"maxLength":1024},"expectedState":{"type":"string","pattern":"^[0-9A-Fa-f]{64}$"}}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileRead | ToolCapability.FileWrite | ToolCapability.Process | ToolCapability.Network);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => states.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await states.CaptureAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (!snapshot.Preview.State.Equals(request.Arguments.GetRequiredString("expectedState"), StringComparison.OrdinalIgnoreCase))
                return ToolResult.Failure(ToolExecutionText.Get("GitPushStateChanged"), "git_state_changed");
            cancellationToken.ThrowIfCancellationRequested();
            var result = await git.RunAsync(snapshot.Repository.Root, snapshot.Repository.Command("-c", "protocol.allow=never",
                "-c", $"protocol.{snapshot.Protocol}.allow=always", "-c", $"remote.{snapshot.Preview.Remote}.mirror=false",
                "-c", "push.pushOption=", "-c", "push.autoSetupRemote=false", "push", "--porcelain",
                "--no-force", "--no-mirror", "--no-prune", "--no-follow-tags", "--no-push-option", "--recurse-submodules=no",
                "--receive-pack=git-receive-pack", "--", snapshot.Preview.Remote, $"{snapshot.Preview.Head}:refs/heads/{snapshot.Preview.TargetBranch}"),
                context, cancellationToken).ConfigureAwait(false);
            return result.IsSuccess ? ToolResult.Success(ToolExecutionText.Get("GitPushSucceeded"))
                : ToolResult.Failure(ToolExecutionText.Get("GitPushFailed"), "git_push_failed");
        }
        catch (GitPushException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }
}
