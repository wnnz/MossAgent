using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Processes;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Git;

public sealed class GitDiffTool(
    AuthorizedPathResolver paths,
    GitCommandRunner git) : IAgentTool, IToolRequestPrevalidator
{
    public ToolDescriptor Descriptor { get; } = new(
        "git.diff", ToolExecutionText.Get("GitDiffDescription"),
        """{"type":"object","properties":{"repository":{"type":"string"},"staged":{"type":"boolean"},"untracked":{"type":"boolean"},"path":{"type":["string","null"]}}}""",
        ToolRiskLevel.ReadOnly, ToolCapability.FileRead | ToolCapability.Process);

    public void ValidateRequest(ToolRequest request, ToolExecutionContext context)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory, context);
        var path = request.Arguments.GetOptionalString("path");
        ValidateMode(request, path);
        if (!string.IsNullOrWhiteSpace(path))
        {
            var resolved = paths.Resolve(Path.GetFullPath(path, repository), context);
            if (request.Arguments.GetOptionalBoolean("untracked") == true && Directory.Exists(resolved))
                throw new ArgumentException();
        }
    }

    public async Task<ToolResult> ExecuteAsync(
        ToolRequest request,
        ToolExecutionContext context,
        CancellationToken cancellationToken)
    {
        var repository = paths.Resolve(
            request.Arguments.GetOptionalString("repository") ?? context.WorkingDirectory,
            context);
        ValidateRequest(request, context);
        var untracked = request.Arguments.GetOptionalBoolean("untracked") == true;
        var arguments = new List<string>
        {
            "-c", "core.quotepath=false", "-c", "core.safecrlf=false", "--literal-pathspecs",
            "diff", "--no-ext-diff", "--no-textconv", "--no-color"
        };
        if (untracked) arguments.Add("--no-index");
        if (request.Arguments.GetOptionalBoolean("staged") == true)
        {
            arguments.Add("--staged");
        }

        var path = request.Arguments.GetOptionalString("path");
        if (!string.IsNullOrWhiteSpace(path))
        {
            paths.Resolve(Path.GetFullPath(path, repository), context);
            arguments.Add("--");
            if (untracked) arguments.Add(OperatingSystem.IsWindows() ? "NUL" : "/dev/null");
            arguments.Add(path);
        }

        var captured = new BoundedTextBuffer(context.MaximumOutputCharacters);
        var result = await git.RunAsync(repository, arguments, context, cancellationToken,
            acceptExitCodeOne: untracked, standardOutput: captured);
        return GitDiffBinding.Bind(result, captured);
    }

    private static void ValidateMode(ToolRequest request, string? path)
    {
        if (request.Arguments.GetOptionalBoolean("untracked") == true &&
            (string.IsNullOrWhiteSpace(path) || request.Arguments.GetOptionalBoolean("staged") == true))
            throw new ArgumentException();
    }
}
