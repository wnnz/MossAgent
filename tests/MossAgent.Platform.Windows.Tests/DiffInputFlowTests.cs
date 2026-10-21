using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealGitProducesPatchOrNumstatWithPinnedInputsAndNoTemporaryResidue(bool statistics)
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync();
        fixture.Inputs.Created = (session, _) =>
        {
            Assert.Throws<IOException>(() => File.Delete(session.Path));
            Assert.Throws<IOException>(() => File.WriteAllText(session.Path, "foreign"));
        };
        var result = await fixture.ExecuteAsync(statistics);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(2, fixture.Inputs.Sessions.Count);
        Assert.All(fixture.Inputs.Sessions, session => { Assert.True(session.IsDisposed); Assert.False(File.Exists(session.Path)); });
        if (statistics)
        {
            var counts = JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!);
            Assert.Equal(new ToolFileChangeStatistics(1, 1), counts);
            Assert.Equal(2, Directory.GetFiles(fixture.Files.ArtifactRoot).Length);
        }
        else
        {
            var patch = await File.ReadAllTextAsync(Assert.Single(result.Artifacts!).Path, fixture.Files.Token);
            Assert.Contains("-handle fixture", patch);
            Assert.Contains("+updated", patch);
            Assert.DoesNotContain("diff-input-", patch);
            Assert.Equal(3, Directory.GetFiles(fixture.Files.ArtifactRoot).Length);
        }
        Assert.Empty(Directory.GetDirectories(fixture.Files.ArtifactRoot));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ReadOnlyQueriesKeepTheirPolicyAndExplicitApprovalRefusalCreatesNoInputs(bool statistics, bool readOnly)
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync();
        var context = fixture.Files.Context with { ApprovalPolicy = readOnly ? ApprovalPolicy.ReadOnly : ApprovalPolicy.AskEveryTime };
        var result = await fixture.ExecuteAsync(statistics, context, approve: readOnly);
        Assert.Equal(readOnly, result.IsSuccess);
        Assert.Equal(readOnly ? null : "approval_denied", result.ErrorCode);
        Assert.Equal(readOnly ? 2 : 0, fixture.Inputs.Sessions.Count);
        Assert.All(fixture.Inputs.Sessions, session => Assert.True(session.IsDisposed));
        Assert.Equal(0, fixture.Approvals);
        Assert.Equal(readOnly && !statistics ? 3 : 2, Directory.GetFiles(fixture.Files.ArtifactRoot).Length);
    }
}
