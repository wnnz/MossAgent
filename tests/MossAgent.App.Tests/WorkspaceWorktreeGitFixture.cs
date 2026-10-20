using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceWorktreeGitFixture : IAsyncDisposable
{
    private readonly WorkspaceModelSelectionFixture _fixture;
    private WorkspaceWorktreeGitFixture(WorkspaceModelSelectionFixture fixture) => _fixture = fixture;
    public WorkspaceModelSelectionFixture Fixture => _fixture;
    public ProjectProfile Project => _fixture.Workspace.Catalog.SelectedProject!;

    public static async Task<WorkspaceWorktreeGitFixture> CreateAsync(bool pauseResponse = false)
    {
        var owner = new WorkspaceWorktreeGitFixture(await WorkspaceModelSelectionFixture.CreateAsync(pauseResponse));
        try
        {
            await owner.GitAsync(["init", "--initial-branch=fixture-main"]);
            await owner.GitAsync(["config", "user.name", "MossAgent fixture"]);
            await owner.GitAsync(["config", "user.email", "fixture@example.invalid"]);
            await owner.GitAsync(["config", "core.hooksPath", Path.Combine(owner.Project.PrimaryDirectory, "disabled-test-hooks")]);
            await File.WriteAllTextAsync(Path.Combine(owner.Project.PrimaryDirectory, ".gitignore"), "data/\n", owner._fixture.Token);
            await File.WriteAllTextAsync(Path.Combine(owner.Project.PrimaryDirectory, "README.md"), "committed baseline\n", owner._fixture.Token);
            await owner.GitAsync(["add", "--", "README.md", ".gitignore"]);
            await owner.GitAsync(["-c", "commit.gpgsign=false", "commit", "-m", "fixture baseline"]);
            owner._fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
            return owner;
        }
        catch
        {
            await owner.DisposeAsync();
            throw;
        }
    }

    public async Task<GitProcessResult> GitAsync(IReadOnlyList<string> arguments, string? directory = null)
    {
        var result = await _fixture.Git.RunAsync(directory ?? Project.PrimaryDirectory, arguments, _fixture.Token);
        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.False(result.OutputTruncated);
        return result;
    }

    public async ValueTask DisposeAsync()
    {
        var root = Path.GetFullPath(Project.PrimaryDirectory);
        var temporary = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests")) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture cleanup must remain inside its temporary root.");
        if (Directory.Exists(root))
            foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await _fixture.DisposeAsync();
    }
}
