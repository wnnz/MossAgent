using Microsoft.Data.Sqlite;
using MossAgent.Application.Persistence;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions.Browser;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteBrowserSessionJournalTests
{
    [Fact]
    public async Task RecordSessionAsync_ReturnsFalseForMissingOrArchivedTasks()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.False(await journal.RecordSessionAsync(
            Guid.NewGuid(), BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        await fixture.ExecuteAsync(
            $"INSERT INTO archived_agent_tasks(task_id,archived_at) VALUES('{fixture.Task.Id}','2024-01-01');");
        Assert.False(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM browser_session_records;"));
    }

    [Fact]
    public async Task RecordSessionAsync_RoundTripsEveryColumn()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Edge, BrowserProfileMode.ExistingCdp, fixture.Token));

        var stored = Assert.Single(await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token));
        Assert.NotEqual(Guid.Empty, stored.SessionId);
        Assert.Equal(fixture.Task.Id, stored.TaskId);
        Assert.Equal(BrowserEngine.Edge, stored.Engine);
        Assert.Equal(BrowserProfileMode.ExistingCdp, stored.ProfileMode);
        Assert.Equal(BrowserSessionState.Attached, stored.State);
        Assert.True(stored.UpdatedAt >= stored.CreatedAt);
    }

    [Fact]
    public async Task RecordActionAsync_RejectsTasksWithoutAnAttachedSession()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.Null(await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Navigate, "a1b2c3d4e5f6a1b2", fixture.Token));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM browser_action_intents;"));
    }

    [Fact]
    public async Task RecordActionAsync_RoundTripsAndBindsToTheAttachedSession()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Chrome, BrowserProfileMode.Managed, fixture.Token));
        var sessionId = (await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token)).Single().SessionId;

        var intentId = await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Click, "deadbeef01234567", fixture.Token);

        Assert.NotNull(intentId);
        var stored = Assert.Single(await journal.ReadIntentsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(intentId.Value, stored.IntentId);
        Assert.Equal(sessionId, stored.SessionId);
        Assert.Equal(BrowserActionKind.Click, stored.Kind);
        Assert.Equal("deadbeef01234567", stored.TargetHash);
        Assert.Equal(BrowserActionIntentState.Committed, stored.State);
        var missing = await journal.RecordActionAsync(
            Guid.NewGuid(), BrowserActionKind.Click, "deadbeef01234567", fixture.Token);
        Assert.Null(missing);
    }

    [Fact]
    public async Task RecordActionAsync_RejectsSessionsThatAlreadyDetached()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        await journal.RecordDetachedAsync(fixture.Task.Id, fixture.Token);

        Assert.Null(await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Reload, null, fixture.Token));
    }

    [Theory]
    [InlineData(BrowserActionIntentState.Resolved)]
    [InlineData(BrowserActionIntentState.Failed)]
    [InlineData(BrowserActionIntentState.Cancelled)]
    [InlineData(BrowserActionIntentState.Interrupted)]
    public async Task ResolveActionAsync_TransitionsCommittedIntentsToTerminalStates(
        BrowserActionIntentState state)
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        var intentId = (await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Type, "0123456789abcdef", fixture.Token))!.Value;

        await journal.ResolveActionAsync(intentId, state, fixture.Token);

        var stored = Assert.Single(await journal.ReadIntentsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(state, stored.State);
        Assert.True(stored.UpdatedAt >= stored.CreatedAt);
    }

    [Fact]
    public async Task ResolveActionAsync_IsIdempotentAndRejectsCommitted()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        var intentId = (await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Navigate, "0123456789abcdef", fixture.Token))!.Value;
        await Assert.ThrowsAsync<ArgumentException>(() =>
            journal.ResolveActionAsync(intentId, BrowserActionIntentState.Committed, fixture.Token));

        await journal.ResolveActionAsync(intentId, BrowserActionIntentState.Resolved, fixture.Token);
        await journal.ResolveActionAsync(intentId, BrowserActionIntentState.Failed, fixture.Token);
        await journal.ResolveActionAsync(intentId, BrowserActionIntentState.Cancelled, fixture.Token);

        var stored = Assert.Single(await journal.ReadIntentsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(BrowserActionIntentState.Resolved, stored.State);
    }

    [Fact]
    public async Task SessionStateMarks_AreIdempotentAndOnlyTouchAttachedRows()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));

        await journal.RecordDisconnectedAsync(fixture.Task.Id, fixture.Token);
        var stored = Assert.Single(await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(BrowserSessionState.Disconnected, stored.State);

        // Detached must not overwrite a disconnected row, and repeats are no-ops.
        await journal.RecordDetachedAsync(fixture.Task.Id, fixture.Token);
        stored = Assert.Single(await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(BrowserSessionState.Disconnected, stored.State);

        await using var second = await SqliteFailurePersistenceFixture.CreateAsync();
        var secondJournal = new SqliteBrowserSessionJournal(second.Connections);
        Assert.True(await secondJournal.RecordSessionAsync(
            second.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, second.Token));
        await secondJournal.RecordDetachedAsync(second.Task.Id, second.Token);
        await secondJournal.RecordDetachedAsync(second.Task.Id, second.Token);
        stored = Assert.Single(await secondJournal.ReadSessionsAsync(second.Task.Id, second.Token));
        Assert.Equal(BrowserSessionState.Detached, stored.State);
        // Terminal marks only touch attached rows: a detached session stays detached.
        await secondJournal.RecordDisconnectedAsync(second.Task.Id, second.Token);
        stored = Assert.Single(await secondJournal.ReadSessionsAsync(second.Task.Id, second.Token));
        Assert.Equal(BrowserSessionState.Detached, stored.State);
    }

    [Fact]
    public async Task DeletingTheTaskCascadesSessionsAndIntents()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Chrome, BrowserProfileMode.DailyProfile, fixture.Token));
        await journal.RecordActionAsync(fixture.Task.Id, BrowserActionKind.NewTab, null, fixture.Token);
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM browser_session_records;"));
        Assert.Equal(1, await fixture.CountAsync("SELECT COUNT(*) FROM browser_action_intents;"));

        await fixture.ExecuteAsync($"DELETE FROM agent_tasks WHERE id='{fixture.Task.Id}';");

        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM browser_session_records;"));
        Assert.Equal(0, await fixture.CountAsync("SELECT COUNT(*) FROM browser_action_intents;"));
    }

    [Fact]
    public async Task ReconcileAsync_MarksOnlyAttachedSessionsAndCommittedIntents()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        var committed = (await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Navigate, "0123456789abcdef", fixture.Token))!.Value;
        var resolved = (await journal.RecordActionAsync(
            fixture.Task.Id, BrowserActionKind.Click, "deadbeef01234567", fixture.Token))!.Value;
        await journal.ResolveActionAsync(resolved, BrowserActionIntentState.Resolved, fixture.Token);
        await using var released = await SqliteFailurePersistenceFixture.CreateAsync();
        var releasedJournal = new SqliteBrowserSessionJournal(released.Connections);
        Assert.True(await releasedJournal.RecordSessionAsync(
            released.Task.Id, BrowserEngine.Chrome, BrowserProfileMode.Managed, released.Token));
        await releasedJournal.RecordDetachedAsync(released.Task.Id, released.Token);

        Assert.Equal(2, await journal.ReconcileAsync(fixture.Token));
        Assert.Equal(0, await releasedJournal.ReconcileAsync(released.Token));

        var session = Assert.Single(await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token));
        Assert.Equal(BrowserSessionState.Interrupted, session.State);
        var intents = await journal.ReadIntentsAsync(fixture.Task.Id, fixture.Token);
        Assert.Equal(BrowserActionIntentState.Interrupted,
            intents.Single(intent => intent.IntentId == committed).State);
        Assert.Equal(BrowserActionIntentState.Resolved,
            intents.Single(intent => intent.IntentId == resolved).State);
        var releasedSession = Assert.Single(
            await releasedJournal.ReadSessionsAsync(released.Task.Id, released.Token));
        Assert.Equal(BrowserSessionState.Detached, releasedSession.State);
        // Reconciliation is idempotent — nothing left to mark.
        Assert.Equal(0, await journal.ReconcileAsync(fixture.Token));
    }

    [Fact]
    public async Task StorageRejectsInvalidKindsAndTargetLengths()
    {
        await using var fixture = await SqliteFailurePersistenceFixture.CreateAsync();
        var journal = new SqliteBrowserSessionJournal(fixture.Connections);
        Assert.True(await journal.RecordSessionAsync(
            fixture.Task.Id, BrowserEngine.Embedded, BrowserProfileMode.Managed, fixture.Token));
        var sessionId = (await journal.ReadSessionsAsync(fixture.Task.Id, fixture.Token)).Single().SessionId;
        var badKind = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_action_intents(id,session_id,kind,target_hash,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{sessionId}}','screenshot',NULL,0,'now','now');
            """));
        Assert.Equal(19, badKind.SqliteErrorCode);
        var badState = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_action_intents(id,session_id,kind,target_hash,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{sessionId}}','click',NULL,5,'now','now');
            """));
        Assert.Equal(19, badState.SqliteErrorCode);
        // A full URL or raw selector exceeds the 16-character hash envelope and is rejected.
        var rawTarget = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_action_intents(id,session_id,kind,target_hash,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{sessionId}}','navigate','https://example.com/secret',0,'now','now');
            """));
        Assert.Equal(19, rawTarget.SqliteErrorCode);
        var missingSession = await Assert.ThrowsAsync<SqliteException>(() => fixture.ExecuteAsync($$"""
            INSERT INTO browser_action_intents(id,session_id,kind,target_hash,state,created_at,updated_at)
            VALUES('{{Guid.NewGuid():D}}','{{Guid.NewGuid():D}}','click',NULL,0,'now','now');
            """));
        Assert.Equal(19, missingSession.SqliteErrorCode);
    }
}
