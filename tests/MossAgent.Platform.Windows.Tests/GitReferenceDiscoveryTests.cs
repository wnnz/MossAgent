using MossAgent.Application.Git;
using MossAgent.Domain;
using MossAgent.Infrastructure.Git;
using MossAgent.Platform.Windows.Processes;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class GitReferenceDiscoveryTests
{
    [Fact]
    public async Task LocalBranchesAndTagsAreBoundedSearchableAndPreviewToCommits()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        var repository = Path.Combine(root, "repository");
        var project = new ProjectProfile(Guid.NewGuid(), "refs", repository, [root], DateTimeOffset.UtcNow);
        var git = new GitProcessRunner(new WindowsTaskProcessLauncher());
        var service = new GitReferenceDiscoveryService(git);
        Directory.CreateDirectory(repository);
        try
        {
            Assert.Equal(0, (await git.RunAsync(repository, ["init"], TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["config", "user.name", "Refs QA"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["config", "user.email", "refs@example.invalid"],
                TestContext.Current.CancellationToken)).ExitCode);
            await File.WriteAllTextAsync(Path.Combine(repository, "README.md"), "reference baseline",
                TestContext.Current.CancellationToken);
            Assert.Equal(0, (await git.RunAsync(repository, ["add", "README.md"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["commit", "-m", "baseline subject"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["branch", "feature/one"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["tag", "v1.0"],
                TestContext.Current.CancellationToken)).ExitCode);
            Assert.Equal(0, (await git.RunAsync(repository, ["tag", "-a", "v1.1", "-m", "annotated"],
                TestContext.Current.CancellationToken)).ExitCode);

            var first = await service.ListLocalAsync(project, string.Empty, 1, 2,
                TestContext.Current.CancellationToken);
            var second = await service.ListLocalAsync(project, string.Empty, 2, 2,
                TestContext.Current.CancellationToken);
            Assert.Equal(2, first.Items.Count);
            Assert.True(first.HasMore);
            Assert.Equal(2, second.Items.Count);
            Assert.Contains(first.Items.Concat(second.Items), item => item.Name == "feature/one");
            Assert.Contains(first.Items.Concat(second.Items), item => item.Name == "v1.0");
            Assert.Contains(first.Items.Concat(second.Items), item => item.Name == "v1.1");

            var branch = Assert.Single((await service.ListLocalAsync(project, "feature", 1, 20,
                TestContext.Current.CancellationToken)).Items);
            Assert.Equal(GitReferenceKind.Branch, branch.Kind);
            var preview = await service.PreviewAsync(project, branch.FullName, TestContext.Current.CancellationToken);
            Assert.Equal(branch.Commit, preview.Commit);
            Assert.Equal("baseline subject", preview.Subject);
            Assert.Equal("Refs QA", preview.Author);

            var tag = Assert.Single((await service.ListLocalAsync(project, "v1.1", 1, 20,
                TestContext.Current.CancellationToken)).Items);
            var tagPreview = await service.PreviewAsync(project, tag.FullName, TestContext.Current.CancellationToken);
            Assert.Equal("baseline subject", tagPreview.Subject);
            Assert.Equal(preview.Commit, tagPreview.Commit);
        }
        finally { await DeleteRootAsync(root); }
    }

    private static async Task DeleteRootAsync(string root)
    {
        if (!Directory.Exists(root)) return;
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, File.GetAttributes(file) & ~FileAttributes.ReadOnly);
        await WindowsProcessTestDirectory.DeleteAsync(root);
    }
}
