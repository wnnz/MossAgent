using MossAgent.Domain;
using MossAgent.Tools.BuiltIn.Execution;
using MossAgent.Tools.BuiltIn.Files;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileRevealBoundaryTests
{
    [Theory]
    [InlineData("preflight")]
    [InlineData("approval")]
    [InlineData("worker")]
    public async Task EscapingJunctionSubstitutionCannotReachShellSelection(string stage)
    {
        using var fixture = new FileEditingFixture();
        using var outside = new FileEditingFixture();
        await outside.SeedAsync("must not reveal");
        var link = Path.Combine(fixture.Root, "folder");
        var target = Path.Combine(link, "edit.txt");
        Directory.CreateDirectory(link);
        await File.WriteAllTextAsync(target, "authorized", fixture.Token);
        async Task ReplaceAsync()
        {
            File.Delete(target);
            Directory.Delete(link);
            await TestDirectoryJunction.CreateAsync(link, outside.Root, fixture.Token);
        }
        var service = new ProbeFileRevealService();
        var tool = new FileRevealTool(fixture.Paths, service);
        var approvals = 0;
        var executor = new ToolExecutor(new ToolRegistry([tool]), new PolicyApprovalService(async (_, _, _) =>
        {
            approvals++;
            if (stage == "approval") await ReplaceAsync();
            return true;
        }));
        if (stage == "preflight") await ReplaceAsync();
        if (stage == "worker") service.BeforeValidation = ReplaceAsync;
        try
        {
            var result = await executor.ExecuteAsync(FileEditingFixture.Request(tool, new { path = target }),
                fixture.Context with { ApprovalPolicy = ApprovalPolicy.AskEveryTime }, fixture.Token);
            Assert.Equal("path_denied", result.ErrorCode);
            Assert.Equal(stage == "preflight" ? 0 : 1, approvals);
            Assert.Empty(service.Paths);
            Assert.Equal("must not reveal", await outside.ContentsAsync());
        }
        finally { if (new DirectoryInfo(link).LinkTarget is not null) Directory.Delete(link); }
    }
}
