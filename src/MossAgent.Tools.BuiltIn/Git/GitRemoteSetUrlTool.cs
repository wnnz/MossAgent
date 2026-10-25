using System.Text.Json;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitRemoteSetUrlTool(GitRemoteUrlReader urls, GitRemoteUrlTarget targets, GitCommandRunner git)
    : IAgentTool, IToolRequestPrevalidator
{
    private const string ExpectationSchema = """{"type":"object","additionalProperties":false,"required":["method"],"properties":{"endpoint":{"type":["string","null"],"minLength":1,"maxLength":32768},"method":{"type":"string","enum":["local","ssh","https","embedded","unsupported"]}}}""";

    public ToolDescriptor Descriptor { get; } = new("git.remote_set_url", ToolExecutionText.Get("GitRemoteSetUrlDescription"),
        $$"""{"type":"object","additionalProperties":false,"required":["remote"],"properties":{"repository":{"type":"string","minLength":1,"maxLength":32768},"remote":{"type":"string","minLength":1,"maxLength":256},"url":{"type":"string","minLength":1,"maxLength":32768},"pushUrl":{"type":"string","minLength":1,"maxLength":32768},"expectedFetch":{{ExpectationSchema}},"expectedPush":{{ExpectationSchema}}},"allOf":[{"anyOf":[{"required":["url"]},{"required":["pushUrl"]}]},{"anyOf":[{"required":["expectedFetch"]},{"required":["expectedPush"]}]}]}""",
        ToolRiskLevel.ExternalSideEffect, ToolCapability.FileWrite | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context) => urls.Validate(request, context);

    public async Task<ToolResult> ExecuteAsync(ToolRequest request, ToolExecutionContext context, CancellationToken cancellationToken)
    {
        try
        {
            var state = await urls.ReadAsync(request, context, cancellationToken).ConfigureAwait(false);
            if (GitRemoteUrlExpectation.Parse(request.Arguments, "expectedFetch")?.Matches(state.Fetch) == false
                || GitRemoteUrlExpectation.Parse(request.Arguments, "expectedPush")?.Matches(state.Push) == false)
                throw new GitRemotesException("git_remote_changed", "GitRemoteChanged");
            var url = request.Arguments.GetOptionalString("url");
            var pushUrl = request.Arguments.GetOptionalString("pushUrl");
            var branch = state.SourceBranch ?? "HEAD";
            if (url is not null) await targets.AuthorizeAsync(url, state.Repository.Root, branch, context, cancellationToken).ConfigureAwait(false);
            if (pushUrl is not null) await targets.AuthorizeAsync(pushUrl, state.Repository.Root, branch, context, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var applied = false;
            if (url is not null)
            {
                applied = await WriteAsync(state, url, false, context, cancellationToken).ConfigureAwait(false);
            }
            if (pushUrl is not null && !await WriteAsync(state, pushUrl, true, context, cancellationToken).ConfigureAwait(false))
                throw new GitRemotesException("git_remote_set_failed", applied ? "GitRemoteSetUrlPartial" : "GitRemoteSetUrlFailed");
            var report = (await urls.ReadAsync(request, context, cancellationToken).ConfigureAwait(false)).Entry();
            return ToolResult.Success(ToolExecutionText.Get("GitRemoteSetUrlSucceeded"),
                JsonSerializer.Serialize(report, JsonSerializerOptions.Web));
        }
        catch (GitRemotesException error) { return ToolResult.Failure(error.Message, error.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }

    // Git's own stderr may echo the URL (including embedded credentials), so failures collapse to a
    // fixed localized message; applied tracks whether the fetch leg already changed so a second-leg
    // failure reports an honest partially applied state.
    private async Task<bool> WriteAsync(GitRemoteUrlState state, string url, bool push,
        ToolExecutionContext context, CancellationToken token)
    {
        var arguments = push ? new[] { "remote", "set-url", "--push", "--", state.Name, url }
            : new[] { "remote", "set-url", "--", state.Name, url };
        var result = await git.RunAsync(state.Repository.Root, state.Repository.Command(arguments), context, token).ConfigureAwait(false);
        if (!result.IsSuccess && !push) throw new GitRemotesException("git_remote_set_failed", "GitRemoteSetUrlFailed");
        return result.IsSuccess;
    }
}
