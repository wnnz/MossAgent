using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

internal sealed class GitHunksFixture : IDisposable
{
    private readonly GitDiffReviewFixture _git = new();
    public string Root => _git.Root;
    public ToolExecutionContext Context => _git.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime };
    public ToolExecutor Executor { get; }
    public int ApprovalRequests { get; private set; }
    public bool Approved { get; set; } = true;

    public GitHunksFixture()
    {
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        var authorization = new GitRepositoryAuthorization(new GitProcessRunner(new FixtureTaskProcessLauncher()));
        var hunks = new GitHunkApplier(paths, authorization, _git.Runner);
        Executor = new(new ToolRegistry([new GitDiffTool(new AuthorizedPathResolver(), _git.Runner),
            new GitStageHunksTool(hunks), new GitUnstageHunksTool(hunks)]),
            new PolicyApprovalService((_, _, _) => { ApprovalRequests++; return ValueTask.FromResult(Approved); }));
    }

    public async Task InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _git.InitializeAsync(token);
        await _git.RunSetupAsync(["config", "core.fsmonitor", "false"], token);
        await _git.RunSetupAsync(["config", "core.hooksPath", Path.Combine(Root, "disabled-test-hooks")], token);
        await _git.RunSetupAsync(["config", "user.name", "MossAgent fixture"], token);
        await _git.RunSetupAsync(["config", "user.email", "fixture@example.invalid"], token);
        await _git.RunSetupAsync(["config", "commit.gpgsign", "false"], token);
    }

    public async Task CommitFileAsync(string name, string content)
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(Path.Combine(Root, name), content, token);
        await GitAsync("add", "--", name);
        await GitAsync("commit", "-qm", "fixture " + name);
    }

    public Task<ToolResult> ExecuteAsync(string tool, object arguments, ToolExecutionContext? context = null) =>
        Executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)),
            context ?? Context, TestContext.Current.CancellationToken);

    public async Task<string> ExpectedDiffAsync(string path, bool staged = false)
    {
        var review = await ExecuteAsync("git.diff", new { path, staged });
        Assert.True(review.IsSuccess, review.Summary);
        return FileHash(review.Content ?? string.Empty, path);
    }

    public async Task<string> FullDiffFileHashAsync(string path, bool staged = false)
    {
        var review = await ExecuteAsync("git.diff", new { staged });
        Assert.True(review.IsSuccess, review.Summary);
        return FileHash(review.Content ?? string.Empty, path);
    }

    public async Task<string> DiffShaAsync(string path, bool staged = false)
    {
        var review = await ExecuteAsync("git.diff", new { path, staged });
        Assert.True(review.IsSuccess, review.Summary);
        var content = review.Content ?? string.Empty;
        var index = content.IndexOf(GitDiffBinding.DiffHashPrefix, StringComparison.Ordinal);
        return index < 0 ? GitDiffBinding.Hash(string.Empty) : content.Substring(index + GitDiffBinding.DiffHashPrefix.Length, 64);
    }

    public static string FileHash(string content, string path)
    {
        var index = 0;
        while ((index = content.IndexOf(GitDiffBinding.FileHashPrefix, index, StringComparison.Ordinal)) >= 0)
        {
            var start = index + GitDiffBinding.FileHashPrefix.Length;
            var lineEnd = content.IndexOf('\n', start);
            if (lineEnd < 0) lineEnd = content.Length;
            var line = content[start..lineEnd];
            if (line.Length > 64 && line[64] == ' ' && line[65..] == path) return line[..64];
            index = lineEnd;
        }

        throw new InvalidOperationException($"No file-sha256 binding for {path} in git.diff output.");
    }

    public async Task<string> GitAsync(params string[] arguments)
    {
        var result = await GitRawAsync(arguments);
        Assert.True(result.IsSuccess, result.Summary);
        return result.Content ?? string.Empty;
    }

    public Task<ToolResult> GitRawAsync(params string[] arguments) =>
        _git.Runner.RunAsync(Root, arguments, Context, TestContext.Current.CancellationToken);

    public void Dispose() => _git.Dispose();
}
