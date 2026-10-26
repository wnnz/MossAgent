using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqlitePullRequestMarksTests
{
    private static readonly string HeadA = new('a', 40);
    private static readonly string HeadB = new('b', 40);

    [Fact]
    public async Task MarksUpsertAndReturnInNumberOrderWithStoredViewedTime()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        var account = Guid.NewGuid();
        var before = DateTimeOffset.UtcNow;
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, HeadA, fixture.Token);
        var stored = Assert.Single(await marks.GetAsync(account, "fixture", "repo", [7], fixture.Token));
        Assert.Equal(account, stored.AccountId);
        Assert.Equal("fixture", stored.Owner);
        Assert.Equal("repo", stored.Repository);
        Assert.Equal(7, stored.Number);
        Assert.Equal(HeadA, stored.HeadRevision);
        // viewed_at persists whole UTC Unix seconds, so compare at that precision.
        Assert.InRange(stored.ViewedAt.ToUnixTimeSeconds(), before.ToUnixTimeSeconds(),
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.Equal(TimeSpan.Zero, stored.ViewedAt.Offset);
        await marks.MarkViewedAsync(account, "fixture", "repo", 8, HeadB, fixture.Token);
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, HeadB, fixture.Token);
        var heads = (await marks.GetAsync(account, "fixture", "repo", [8, 7, 7], fixture.Token))
            .Select(mark => (mark.Number, mark.HeadRevision)).ToArray();
        Assert.Equal([(7, HeadB), (8, HeadB)], heads);
    }

    [Fact]
    public async Task MarksStayScopedToTheirAccountAndRepository()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        var account = Guid.NewGuid();
        var other = Guid.NewGuid();
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, HeadA, fixture.Token);
        Assert.Empty(await marks.GetAsync(other, "fixture", "repo", [7], fixture.Token));
        Assert.Empty(await marks.GetAsync(account, "other", "repo", [7], fixture.Token));
        Assert.Empty(await marks.GetAsync(account, "fixture", "other", [7], fixture.Token));
        Assert.Single(await marks.GetAsync(account, "fixture", "repo", [7], fixture.Token));
    }

    [Fact]
    public async Task GetAsyncIsBoundedAndValidated()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        var account = Guid.NewGuid();
        Assert.Empty(await marks.GetAsync(account, "fixture", "repo", [], fixture.Token));
        var tooMany = Enumerable.Range(1, SqlitePullRequestMarks.MaximumNumbers + 1).ToArray();
        var bounded = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.GetAsync(account, "fixture", "repo", tooMany, fixture.Token));
        Assert.Equal("invalid_arguments", bounded.ErrorCode);
        var zero = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.GetAsync(account, "fixture", "repo", [0], fixture.Token));
        Assert.Equal("invalid_arguments", zero.ErrorCode);
        var badOwner = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.GetAsync(account, "Fixture", "repo", [7], fixture.Token));
        Assert.Equal("invalid_arguments", badOwner.ErrorCode);
        var badSegment = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.GetAsync(account, "fixture/repo", "repo", [7], fixture.Token));
        Assert.Equal("invalid_arguments", badSegment.ErrorCode);
        var emptyAccount = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.GetAsync(Guid.Empty, "fixture", "repo", [7], fixture.Token));
        Assert.Equal("invalid_arguments", emptyAccount.ErrorCode);
        await marks.MarkViewedAsync(account, "fixture", "repo", 7, HeadA, fixture.Token);
        Assert.Single(await marks.GetAsync(account, "fixture", "repo",
            Enumerable.Range(1, SqlitePullRequestMarks.MaximumNumbers).ToArray(), fixture.Token));
    }

    [Fact]
    public async Task MarkViewedRejectsInvalidArgumentsWithoutTouchingStorage()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        var account = Guid.NewGuid();
        foreach (var (number, head) in new[] { (0, HeadA), (-3, HeadA), (7, HeadA[..39]),
            (7, HeadA + "x"), (7, new string('z', 40)), (7, new string('g', 64)) })
        {
            var failure = await Assert.ThrowsAsync<SourceReviewException>(
                () => marks.MarkViewedAsync(account, "fixture", "repo", number, head, fixture.Token));
            Assert.Equal("invalid_arguments", failure.ErrorCode);
        }
        var owner = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.MarkViewedAsync(account, "", "repo", 7, HeadA, fixture.Token));
        Assert.Equal("invalid_arguments", owner.ErrorCode);
        var repository = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.MarkViewedAsync(account, "fixture", "a/b", 7, HeadA, fixture.Token));
        Assert.Equal("invalid_arguments", repository.ErrorCode);
        var missing = await Assert.ThrowsAsync<SourceReviewException>(
            () => marks.MarkViewedAsync(Guid.Empty, "fixture", "repo", 7, HeadA, fixture.Token));
        Assert.Equal("invalid_arguments", missing.ErrorCode);
        Assert.Empty(await marks.GetAsync(account, "fixture", "repo", [7], fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM pull_request_marks;"));
    }

    [Fact]
    public async Task CancellationStopsReadsAndWrites()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var marks = new SqlitePullRequestMarks(fixture.Connections);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(fixture.Token);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => marks.GetAsync(Guid.NewGuid(), "fixture", "repo", [7], cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => marks.MarkViewedAsync(Guid.NewGuid(), "fixture", "repo", 7, HeadA, cancellation.Token));
    }
}
