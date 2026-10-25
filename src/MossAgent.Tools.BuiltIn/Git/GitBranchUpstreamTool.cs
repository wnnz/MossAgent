using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitBranchUpstreamTool(GitBranchUpstreamReader upstreams, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new("git.branch_upstream", ToolExecutionText.Get("GitBranchUpstreamDescription"),
        """{"type":"object","additionalProperties":false,"required":["remote","mergeRef"],"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"branch":{"type":"string","minLength":1,"maxLength":1024},"remote":{"type":"string","minLength":1,"maxLength":256},"mergeRef":{"type":"string","pattern":"^refs/heads/.","maxLength":1080},"expectedRemote":{"type":["string","null"],"minLength":1,"maxLength":256},"expectedMergeRef":{"type":["string","null"],"minLength":1,"maxLength":1080}}}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => upstreams.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var state = await upstreams.ReadAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (Mismatched(request.Arguments, "expectedRemote", state.Remote)
                || Mismatched(request.Arguments, "expectedMergeRef", state.Merge))
                throw new GitRemotesException("git_upstream_changed", "GitUpstreamChanged");
            cancellationToken.ThrowIfCancellationRequested();
            var remote = request.Arguments.GetRequiredString("remote");
            var merge = request.Arguments.GetRequiredString("mergeRef");
            if (!await WriteAsync(state, $"branch.{state.Branch}.remote", remote, context, cancellationToken).ConfigureAwait(false))
                throw new GitRemotesException("git_upstream_failed", "GitUpstreamFailed");
            if (!await WriteAsync(state, $"branch.{state.Branch}.merge", merge, context, cancellationToken).ConfigureAwait(false))
                throw new GitRemotesException("git_upstream_failed", "GitUpstreamPartial");
            var report = new GitRemotesUpstream(state.Branch, remote, merge, null);
            return ToolResult.Success(ToolExecutionText.Format("GitUpstreamSucceeded", state.Branch),
                JsonSerializer.Serialize(report, JsonSerializerOptions.Web));
        }
        catch (GitRemotesException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }

    // A supplied null asserts the config key is absent, a string asserts equality, and a missing
    // property skips the check entirely; upstream keys are not secrets so they compare by raw value.
    private static bool Mismatched(JsonElement arguments, string name, string? actual) =>
        arguments.TryGetProperty(name, out var value)
        && (value.ValueKind == JsonValueKind.String ? value.GetString() != actual : actual is not null);

    private async Task<bool> WriteAsync(GitBranchUpstreamState state, string key, string value,
        ToolExecutionContext context, CancellationToken token)
    {
        var result = await git.RunAsync(state.Repository.Root, state.Repository.Command("config", "--", key, value),
            context, token).ConfigureAwait(false);
        return result.IsSuccess;
    }
}
