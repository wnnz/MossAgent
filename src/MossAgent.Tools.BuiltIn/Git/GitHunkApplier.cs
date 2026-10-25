using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Processes;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitHunkApplier(GitIndexPaths paths, IGitRepositoryAuthorization repositories, GitCommandRunner git)
{
    private const int DiffCaptureCharacters = 256 * 1024;

    public void Validate(ToolRequest request, ToolExecutionContext context)
    {
        paths.Validate(request, context);
        GitHunkOrdinals.Read(request.Arguments);
    }

    public async Task<ToolResult> ExecuteAsync(bool unstage, ToolRequest request, ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        try
        {
            var repository = await repositories.AuthorizeAsync(paths.Directory(request, context), context, cancellationToken);
            var relative = paths.ResolveFiles(request, context, repository.Root).Single()
                .Replace(Path.DirectorySeparatorChar, '/');
            var capture = new BoundedTextBuffer(Math.Max(context.MaximumOutputCharacters, DiffCaptureCharacters));
            var diff = await git.RunAsync(repository.Root, repository.Command(DiffArguments(unstage, relative)),
                context, cancellationToken, standardOutput: capture);
            if (!diff.IsSuccess) return diff;
            if (capture.IsTruncated)
                return ToolResult.Failure(ToolExecutionText.Get("GitHunksTruncated"), "git_hunks_unsupported");
            var output = capture.ToString();
            if (string.IsNullOrWhiteSpace(output))
                return ToolResult.Failure(ToolExecutionText.Get("GitHunksNone"), "git_hunks_none");
            var patch = GitHunkPatchBuilder.Build(output, GitHunkOrdinals.Read(request.Arguments));
            cancellationToken.ThrowIfCancellationRequested();
            var applyArguments = unstage
                ? new[] { "apply", "--cached", "--whitespace=nowarn", "--reverse", "-" }
                : new[] { "apply", "--cached", "--whitespace=nowarn", "-" };
            var apply = await git.RunAsync(repository.Root, repository.Command(applyArguments),
                context, cancellationToken, standardInput: patch.Patch);
            if (!apply.IsSuccess) return apply;
            return ToolResult.Success(
                ToolExecutionText.Format(unstage ? "GitHunksUnstaged" : "GitHunksStaged",
                    patch.SelectedHunks, patch.TotalHunks),
                patch.Patch);
        }
        catch (GitHunksException failure) { return ToolResult.Failure(failure.Message, failure.Code); }
        catch (TimeoutException) { return ToolResult.Failure(ToolExecutionText.Get("ExecutionTimedOut"), "tool_timeout"); }
    }

    private static string[] DiffArguments(bool unstage, string relative)
    {
        var arguments = new List<string>
        {
            "-c", "core.quotepath=false", "-c", "core.safecrlf=false",
            "diff", "--no-ext-diff", "--no-textconv", "--no-color"
        };
        if (unstage) arguments.Add("--staged");
        arguments.Add("--");
        arguments.Add(relative);
        return [.. arguments];
    }
}
