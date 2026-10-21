using MossAgent.Tools.BuiltIn.Execution;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class ReadHandleConcurrencyTests
{
    [Fact]
    public async Task SharedExecutor_ValidatesSeparateRootsAndCancelsOnlyOneRead()
    {
        using var left = new FileEditingFixture();
        using var right = new FileEditingFixture();
        await left.SeedAsync("left");
        await right.SeedAsync("right");
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(left.Token);
        using var bothOpened = new Barrier(2);
        var guard = new ReadHandleProbe { Check = (stream, roots) =>
        {
            var root = Assert.Single(roots);
            Assert.StartsWith(root + Path.DirectorySeparatorChar, stream.Name, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, stream.Position);
            Assert.True(bothOpened.SignalAndWait(TimeSpan.FromSeconds(5), left.Token));
            if (root == left.Root) cancellation.Cancel();
        } };
        var executor = new ToolExecutor(new([left.Read]),
            new PolicyApprovalService(static (_, _, _) => ValueTask.FromResult(true)), readHandleValidator: guard);
        var first = Task.Run(() => executor.ExecuteAsync(FileEditingFixture.Request(left.Read, new { path = left.Path }),
            left.Context, cancellation.Token), left.Token);
        var second = Task.Run(() => executor.ExecuteAsync(FileEditingFixture.Request(right.Read, new { path = right.Path }),
            right.Context, right.Token), right.Token);
        var results = await Task.WhenAll(first, second);
        Assert.Equal("tool_cancelled", results[0].ErrorCode);
        Assert.Null(results[0].Content);
        Assert.True(results[1].IsSuccess, results[1].Summary);
        Assert.Equal("right", results[1].Content);
        Assert.Equal(2, guard.Calls);
        left.AssertNoTemporaryFiles();
        right.AssertNoTemporaryFiles();
    }
}
