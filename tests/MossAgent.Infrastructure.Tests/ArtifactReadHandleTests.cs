using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using MossAgent.Tools.Tests;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class ArtifactReadHandleTests
{
    [Fact]
    public async Task ContentIsReadOnlyAfterValidationOfExactlyTheOwningTaskDirectory()
    {
        using var fixture = new ArtifactReadHandleFixture();
        var probe = new ReadHandleProbe { Check = (stream, roots) =>
        {
            Assert.Equal(0, stream.Position);
            Assert.Equal(fixture.Directory, Assert.Single(roots));
            Assert.True(stream.CanRead);
        } };
        var reader = new TaskArtifactContentReader(fixture.Paths, probe);
        Assert.Equal(fixture.Bytes, await reader.ReadAsync(fixture.TaskId, fixture.Artifact, fixture.Token));
        Assert.Equal(1, probe.Calls);
        Assert.False(probe.Stream!.CanRead);
    }

    [Fact]
    public async Task RefusedHandleIsClosedWithoutReadingOrRetrying()
    {
        using var fixture = new ArtifactReadHandleFixture();
        var probe = new ReadHandleProbe { Check = (stream, _) =>
        {
            Assert.Equal(0, stream.Position);
            throw new UnauthorizedAccessException("synthetic native refusal");
        } };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => new TaskArtifactContentReader(fixture.Paths, probe)
            .ReadAsync(fixture.TaskId, fixture.Artifact, fixture.Token));
        Assert.Equal(1, probe.Calls);
        Assert.False(probe.Stream!.CanRead);
    }

    [Theory]
    [InlineData("wrong-task")]
    [InlineData("negative")]
    [InlineData("oversized")]
    [InlineData("cancelled")]
    public async Task InvalidMetadataOrTaskAndPrecancelledRequestsNeverOpenAHandle(string scenario)
    {
        using var fixture = new ArtifactReadHandleFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var probe = new ReadHandleProbe();
        var artifact = fixture.Artifact with { Length = scenario == "negative" ? -1 : scenario == "oversized" ? FilePreviewFormat.MaximumBytes + 1 : fixture.Artifact.Length };
        if (scenario == "cancelled") cancellation.Cancel();
        var operation = () => new TaskArtifactContentReader(fixture.Paths, probe).ReadAsync(
            scenario == "wrong-task" ? Guid.NewGuid() : fixture.TaskId, artifact, cancellation.Token);
        if (scenario == "wrong-task") await Assert.ThrowsAsync<UnauthorizedAccessException>(operation);
        else if (scenario == "cancelled") await Assert.ThrowsAnyAsync<OperationCanceledException>(operation);
        else await Assert.ThrowsAsync<InvalidDataException>(operation);
        Assert.Equal(0, probe.Calls);
    }

    [Fact]
    public async Task CancellationAtValidationClosesHandleBeforeReading()
    {
        using var fixture = new ArtifactReadHandleFixture();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        var probe = new ReadHandleProbe { Check = (stream, _) => { Assert.Equal(0, stream.Position); cancellation.Cancel(); } };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new TaskArtifactContentReader(fixture.Paths, probe)
            .ReadAsync(fixture.TaskId, fixture.Artifact, cancellation.Token));
        Assert.False(probe.Stream!.CanRead);
    }
}
