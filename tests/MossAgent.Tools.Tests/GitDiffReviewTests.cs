using System.Security.Cryptography;
using System.Text;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.BuiltIn.Git;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class GitDiffReviewTests
{
    private static string Sha256Hex(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    [Fact]
    public async Task UntrackedTextReturnsAnAddedPatchWithoutStagingTheFile()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "新文件.txt"), "first\nsecond\n", token);
        var result = await fixture.ExecuteAsync(new { path = "新文件.txt", untracked = true }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("--- /dev/null", result.Content);
        Assert.Contains("+first", result.Content);
        Assert.Contains("+second", result.Content);
        Assert.Contains("新文件.txt", result.Content);
        var index = await fixture.Runner.RunAsync(fixture.Root, ["ls-files"], fixture.Context, token);
        Assert.Equal(string.Empty, index.Content);
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task UntrackedStatusListsNestedFilesInsteadOfDirectoryPlaceholders()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        Directory.CreateDirectory(Path.Combine(fixture.Root, "new-folder"));
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "new-folder", "新文件.txt"), "new\n", token);
        var result = await fixture.ExecuteAsync(new { nullTerminated = true }, token, toolName: "git.status");
        Assert.Contains("?? new-folder/新文件.txt\0", result.Content);
    }

    [Fact]
    public async Task WindowsLineEndingWarningsDoNotTurnAValidAddedPatchIntoFailure()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await fixture.RunSetupAsync(["config", "core.autocrlf", "true"], token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "lf-file.txt"), "first\nsecond\n", token);
        var result = await fixture.ExecuteAsync(new { path = "lf-file.txt", untracked = true }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("+first", result.Content);
    }

    [Fact]
    public async Task MissingUntrackedFileIsNotMistakenForDiffExitCodeOne()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        var result = await fixture.ExecuteAsync(new { path = "missing.txt", untracked = true }, token);
        Assert.False(result.IsSuccess);
        Assert.Equal("git_failed", result.ErrorCode);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(".", false)]
    [InlineData("new.txt", true)]
    public async Task UntrackedModeRequiresASingleFileAndCannotBeStaged(string? path, bool staged)
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        var result = await fixture.ExecuteAsync(new { path, staged, untracked = true }, token);
        Assert.False(result.IsSuccess);
        Assert.Equal("invalid_arguments", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task UnauthorizedUntrackedPathIsRejectedBeforeApproval()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        var path = Path.Combine(Path.GetDirectoryName(fixture.Root)!, "outside.txt");
        var result = await fixture.ExecuteAsync(new { path, untracked = true }, token,
            fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime });
        Assert.Equal("path_denied", result.ErrorCode);
        Assert.Equal(0, fixture.ApprovalRequests);
    }

    [Fact]
    public async Task TrackedReviewUsesLiteralFilenamesAndDoesNotInvokeTextConversion()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "[ab].cs"), "literal-old\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.cs"), "pattern-old\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, ".gitattributes"), "*.cs diff=review-fixture\n", token);
        await fixture.RunSetupAsync(["add", "--", "[ab].cs", "a.cs", ".gitattributes"], token);
        await fixture.RunSetupAsync(["-c", "user.name=QA", "-c", "user.email=qa@example.invalid", "commit", "-m", "baseline"], token);
        await fixture.RunSetupAsync(["config", "diff.review-fixture.textconv", "git --version"], token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "[ab].cs"), "literal-new\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.cs"), "pattern-new\n", token);
        var result = await fixture.ExecuteAsync(new { path = "[ab].cs" }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("+literal-new", result.Content);
        Assert.DoesNotContain("pattern-new", result.Content);
    }

    [Fact]
    public async Task UntrackedBinaryReviewUsesGitBinaryPresentation()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await File.WriteAllBytesAsync(Path.Combine(fixture.Root, "new.bin"), [0, 1, 2, 3], token);
        var result = await fixture.ExecuteAsync(new { path = "new.bin", untracked = true }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Contains("Binary files", result.Content);
    }

    [Fact]
    public async Task UntrackedPatchStillObeysThePipelineOutputBudget()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "large.txt"), new string('x', 20000), token);
        var result = await fixture.ExecuteAsync(new { path = "large.txt", untracked = true }, token,
            fixture.Context with { MaximumOutputCharacters = 512 });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.InRange(result.OutputCharacterCount, 1, 512);
    }

    private static async Task CommitBaselineAsync(GitDiffReviewFixture fixture, string name, CancellationToken token)
    {
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, name), "one\ntwo\nthree\n", token);
        await fixture.RunSetupAsync(["add", "--", name], token);
        await fixture.RunSetupAsync(["-c", "user.name=QA", "-c", "user.email=qa@example.invalid", "commit", "-qm", "baseline"], token);
    }

    private static string BindingBody(ToolResult result)
    {
        var content = result.Content ?? string.Empty;
        var marker = content.IndexOf(GitDiffBinding.Marker, StringComparison.Ordinal);
        Assert.True(marker > 0, "git.diff output should end with the --- mossagent --- binding block.");
        return content[..marker];
    }

    [Fact]
    public async Task DiffOutputEndsWithVerifiableBindingBlock()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await CommitBaselineAsync(fixture, "tracked.txt", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "tracked.txt"), "one\nTWO\nthree\n", token);

        var result = await fixture.ExecuteAsync(new { path = "tracked.txt" }, token);
        Assert.True(result.IsSuccess, result.Summary);
        var body = BindingBody(result);
        var sha = Sha256Hex(body);
        var expected = body + $"--- mossagent ---\ndiff-sha256:{sha}\nfile-sha256:{sha} tracked.txt\n";
        Assert.Equal(expected, result.Content);
    }

    [Fact]
    public async Task WholeRepoDiffListsEachFileHash()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await CommitBaselineAsync(fixture, "a.txt", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "b.txt"), "x\ny\n", token);
        await fixture.RunSetupAsync(["add", "--", "b.txt"], token);
        await fixture.RunSetupAsync(["-c", "user.name=QA", "-c", "user.email=qa@example.invalid", "commit", "-qm", "second"], token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "a.txt"), "one\nTWO\nthree\n", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "b.txt"), "x\nY\n", token);

        var result = await fixture.ExecuteAsync(new { }, token);
        Assert.True(result.IsSuccess, result.Summary);
        var body = BindingBody(result);
        var parts = body.Split("\ndiff --git ");
        Assert.StartsWith("diff --git ", parts[0]);
        Assert.Equal(2, parts.Length);
        var segments = parts.Select((part, index) =>
            (index == 0 ? "" : "diff --git ") + part + (index < parts.Length - 1 ? "\n" : "")).ToArray();
        var expected = body + "--- mossagent ---\n"
            + $"diff-sha256:{Sha256Hex(body)}\n"
            + $"file-sha256:{Sha256Hex(segments[0])} a.txt\n"
            + $"file-sha256:{Sha256Hex(segments[1])} b.txt\n";
        Assert.Equal(expected, result.Content);
    }

    [Fact]
    public async Task StagedDiffAlsoCarriesTheBindingBlock()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await CommitBaselineAsync(fixture, "tracked.txt", token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "tracked.txt"), "one\nTWO\nthree\n", token);
        await fixture.RunSetupAsync(["add", "--", "tracked.txt"], token);

        var result = await fixture.ExecuteAsync(new { path = "tracked.txt", staged = true }, token);
        Assert.True(result.IsSuccess, result.Summary);
        var body = BindingBody(result);
        Assert.Contains("+TWO", body);
        var sha = Sha256Hex(body);
        Assert.EndsWith($"diff-sha256:{sha}\nfile-sha256:{sha} tracked.txt\n", result.Content);
    }

    [Fact]
    public async Task TruncatedDiffOmitsTheBindingBlock()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "large.txt"), new string('x', 20000), token);
        var result = await fixture.ExecuteAsync(new { path = "large.txt", untracked = true }, token,
            fixture.Context with { MaximumOutputCharacters = 512 });
        Assert.True(result.IsSuccess, result.Summary);
        Assert.DoesNotContain("mossagent", result.Content);
    }

    [Fact]
    public async Task EmptyDiffOmitsTheBindingBlock()
    {
        using var fixture = new GitDiffReviewFixture();
        var token = TestContext.Current.CancellationToken;
        await fixture.InitializeAsync(token);
        await CommitBaselineAsync(fixture, "tracked.txt", token);
        var result = await fixture.ExecuteAsync(new { path = "tracked.txt" }, token);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.True(string.IsNullOrWhiteSpace(result.Content));
    }
}
