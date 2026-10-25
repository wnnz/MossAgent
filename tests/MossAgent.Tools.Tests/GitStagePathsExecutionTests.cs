using System.Text.Json;
using Json.Schema;
using MossAgent.Testing;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Files;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitStagePathsExecutionTests
{
    [Fact]
    public void DescriptorsExposeParseableSchemasAndSideEffectingCapabilities()
    {
        var paths = new GitIndexPaths(new AuthorizedPathResolver());
        var repository = new GitIndexRepositoryProbe();
        var runner = new GitCommandRunner(new RecordingTaskProcessLauncher(() => new StubTaskProcess()));
        IAgentTool[] tools = [new GitStagePathsTool(paths, repository, runner), new GitUnstagePathsTool(paths, repository, runner)];
        Assert.Equal(new[] { "git.stage_paths", "git.unstage_paths" }, tools.Select(tool => tool.Descriptor.Name));
        foreach (var tool in tools)
        {
            Assert.Equal(ToolRiskLevel.ExternalSideEffect, tool.Descriptor.RiskLevel);
            Assert.Equal(ToolCapability.FileWrite | ToolCapability.Process, tool.Descriptor.RequiredCapabilities);
            Assert.False(string.IsNullOrWhiteSpace(tool.Descriptor.Description));
            using var document = JsonDocument.Parse(tool.Descriptor.InputSchemaJson);
            Assert.NotNull(JsonSchema.Build(document.RootElement));
            var schema = document.RootElement;
            Assert.Equal("false", schema.GetProperty("additionalProperties").GetRawText());
            Assert.Contains("paths", schema.GetProperty("required").EnumerateArray().Select(item => item.GetString()));
            var items = schema.GetProperty("properties").GetProperty("paths");
            Assert.Equal(1, items.GetProperty("minItems").GetInt32());
            Assert.Equal(64, items.GetProperty("maxItems").GetInt32());
            Assert.Equal("string", items.GetProperty("items").GetProperty("type").GetString());
        }
    }

    [Theory]
    [InlineData("git.stage_paths", new[] { "add" })]
    [InlineData("git.unstage_paths", new[] { "reset", "--quiet" })]
    public async Task SingleCommandAtomicallyTargetsTheDeduplicatedLiteralGroup(string tool, string[] verbs)
    {
        using var fixture = new GitIndexExecutionFixture();
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken, arguments:
            new { paths = new[] { "[1].txt", "old name.txt", "./[1].txt", "子/文件.txt" } });
        Assert.True(result.IsSuccess, result.Summary);
        var request = Assert.Single(fixture.Launcher.Requests);
        Assert.Equal(fixture.Root, request.WorkingDirectory);
        Assert.Equal(new[] { "--git-dir=" + Path.Combine(fixture.Root, ".git"), "--work-tree=" + fixture.Root, "--literal-pathspecs" },
            request.Arguments.Take(3));
        Assert.Equal([.. verbs, "--", "[1].txt", "old name.txt", Path.Combine("子", "文件.txt")], request.Arguments.Skip(3));
        Assert.Equal(1, fixture.Approvals);
        Assert.Equal(1, fixture.Repository.Calls);
        Assert.True(fixture.Process.Disposed);
        Assert.True(fixture.Audit.Completion?.IsSuccess);
    }

    [Theory]
    [InlineData("git.stage_paths")]
    [InlineData("git.unstage_paths")]
    public async Task InvalidElementDeniesTheWholeGroupBeforeApprovalAndLaunch(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken, arguments:
            new { paths = new[] { "ok.txt", "../escape.txt" } });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.Approvals);
        Assert.Equal(0, fixture.Repository.Calls);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_paths")]
    [InlineData("git.unstage_paths")]
    public async Task MalformedGroupArgumentsAreRejectedBeforeApprovalOrLaunch(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        var token = TestContext.Current.CancellationToken;
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, token, arguments: new { path = "x.txt" })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, token, arguments: new { paths = Array.Empty<string>() })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, token,
            arguments: new { paths = new object[] { "x.txt", 7 } })).ErrorCode);
        Assert.Equal("invalid_arguments", (await fixture.ExecuteAsync(tool, token,
            arguments: new { paths = Enumerable.Range(0, 65).Select(n => $"f{n}.txt").ToArray() })).ErrorCode);
        Assert.Equal(0, fixture.Approvals);
        Assert.Equal(0, fixture.Repository.Calls);
        Assert.Empty(fixture.Launcher.Requests);
    }

    [Theory]
    [InlineData("git.stage_paths")]
    [InlineData("git.unstage_paths")]
    public async Task RepositoryAuthorizationTimeoutIsSanitizedAndAudited(string tool)
    {
        using var fixture = new GitIndexExecutionFixture();
        fixture.Repository.Authorize = (_, _) => throw new TimeoutException("secret-token");
        var result = await fixture.ExecuteAsync(tool, TestContext.Current.CancellationToken,
            arguments: new { paths = new[] { "selected.txt" } });
        Assert.Equal("tool_timeout", result.ErrorCode);
        Assert.DoesNotContain("secret-token", result.Summary);
        Assert.Empty(fixture.Launcher.Requests);
        Assert.Equal("tool_timeout", fixture.Audit.Completion?.ErrorCode);
    }
}
