using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Abstractions.Git;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitEnvironmentIsolationTests
{
    [Fact]
    public async Task GitEnvironmentCannotRedirectStagingCommitOrTraceOutsideTheAuthorizedRepository()
    {
        using var fixture = new GitIndexFixture();
        await fixture.InitializeAsync();
        var outside = Directory.CreateTempSubdirectory("MossAgent-git-env-").FullName;
        try
        {
            var index = Path.Combine(outside, "index");
            var trace = Path.Combine(outside, "trace.log");
            File.Copy(Path.Combine(fixture.Root, ".git", "index"), index);
            var before = await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken);
            var launcher = new GitEnvironmentOverrideLauncher(index, trace);
            var authorization = new GitRepositoryAuthorization(new GitProcessRunner(launcher));
            var git = new GitCommandRunner(launcher);
            var states = new GitCommitStateReader(new AuthorizedPathResolver(), authorization, git);
            var executor = new ToolExecutor(new([new GitStageFileTool(new(new AuthorizedPathResolver()), authorization, git),
                new GitCommitPreviewTool(states), new GitCommitTool(states, git)]), new PolicyApprovalService((_, _, _) => ValueTask.FromResult(true)));
            var context = fixture.Context with { ApprovalPolicy = ApprovalPolicy.FullAccess };
            await File.WriteAllTextAsync(Path.Combine(fixture.Root, "selected.txt"), "authorized index only\n", TestContext.Current.CancellationToken);
            var staged = await ExecuteAsync(executor, context, "git.stage_file", new { path = "selected.txt" });
            Assert.True(staged.IsSuccess, staged.Summary);
            var read = await ExecuteAsync(executor, context, "git.commit_preview", new { });
            Assert.True(read.IsSuccess, read.Summary);
            var preview = JsonSerializer.Deserialize<GitCommitPreview>(read.Content!, JsonSerializerOptions.Web)!;
            Assert.Equal(1, preview.StagedFiles);
            var committed = await ExecuteAsync(executor, context, "git.commit", new { message = "isolated Git environment", expectedState = preview.State });
            Assert.True(committed.IsSuccess, committed.Summary);
            Assert.Equal("authorized index only\n", await fixture.GitAsync("show", "HEAD:selected.txt"));
            Assert.Equal(before, await File.ReadAllBytesAsync(index, TestContext.Current.CancellationToken));
            Assert.False(File.Exists(trace));
            Assert.All(launcher.Requests, request => Assert.Equal(new[] { "GIT_" }, request.RemovedEnvironmentPrefixes));
        }
        finally
        {
            var parent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
            Assert.StartsWith(parent, Path.GetFullPath(outside), StringComparison.OrdinalIgnoreCase);
            Directory.Delete(outside, recursive: true);
        }
    }

    private static Task<ToolResult> ExecuteAsync(IToolExecutor executor, ToolExecutionContext context, string tool, object arguments) =>
        executor.ExecuteAsync(new(Guid.NewGuid().ToString("N"), tool, JsonSerializer.SerializeToElement(arguments)), context, TestContext.Current.CancellationToken);
}
