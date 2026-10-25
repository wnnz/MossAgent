using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using MossAgent.Application.Attachments;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Infrastructure.Attachments;

public sealed class AttachmentSnapshotReaper(SqliteConnectionFactory connections, AppDataPaths paths,
    AttachmentSnapshotLeases? leases = null, TimeProvider? timeProvider = null, TimeSpan? gracePeriod = null)
    : IAttachmentSnapshotReaper
{
    internal const int MaximumDeletionsPerPass = 512;
    private static readonly TimeSpan JournalRetention = TimeSpan.FromDays(30);
    private readonly SemaphoreSlim _pass = new(1, 1);
    private readonly AttachmentSnapshotLeases? _leases = leases;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly TimeSpan _grace = gracePeriod ?? TimeSpan.FromHours(24);

    public Task<AttachmentReclaimSummary> ReclaimAsync(CancellationToken cancellationToken) =>
        RunExclusiveAsync(token => ReclaimCoreAsync(null, token), cancellationToken);

    internal Task<AttachmentReclaimSummary> ReclaimScopeAsync(ConversationDraftScope scope,
        CancellationToken cancellationToken) => RunExclusiveAsync(
        token => ReclaimCoreAsync(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope.Key))), token),
        cancellationToken);

    internal void QueuePass(ConversationDraftScope? scope)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                if (scope is { } value) await ReclaimScopeAsync(value, CancellationToken.None).ConfigureAwait(false);
                else await ReclaimAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort background pass; failures are journaled per file.
            }
        });
    }

    private async Task<AttachmentReclaimSummary> RunExclusiveAsync(
        Func<CancellationToken, Task<AttachmentReclaimSummary>> pass, CancellationToken cancellationToken)
    {
        if (!await _pass.WaitAsync(0, cancellationToken)) return AttachmentReclaimSummary.None;
        try { return await pass(cancellationToken); }
        finally { _pass.Release(); }
    }

    private async Task<AttachmentReclaimSummary> ReclaimCoreAsync(string? scopeHash, CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var cutoff = now - _grace;
        await AttachmentReclaimIntents.PruneAsync(connection, now - JournalRetention, cancellationToken);
        await ReconcileIntentsAsync(connection, now, cancellationToken);
        var scanned = 0;
        var reclaimed = 0;
        var retained = 0;
        var failed = 0;
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in AttachmentSnapshotCandidates.Enumerate(paths, scopeHash))
        {
            cancellationToken.ThrowIfCancellationRequested();
            scanned++;
            directories.Add(candidate.DirectoryPath);
            if (reclaimed >= MaximumDeletionsPerPass) { retained++; continue; }
            var outcome = await ReclaimCandidateAsync(connection, candidate, cutoff, now, cancellationToken);
            switch (outcome)
            {
                case true: reclaimed++; break;
                case false: retained++; break;
                default: failed++; break;
            }
        }
        PruneEmptyDirectories(directories);
        return new(scanned, reclaimed, retained, failed);
    }

    private async Task ReconcileIntentsAsync(SqliteConnection connection, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        foreach (var recorded in await AttachmentReclaimIntents.ReadRecordedAsync(connection, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(recorded)) continue;
            try
            {
                await AttachmentReclaimIntents.MarkAsync(connection, recorded, AttachmentReclaimStage.Reclaimed,
                    null, now, cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or SqliteException or UnauthorizedAccessException)
            {
                // Journal updates are best-effort; the next pass retries.
            }
        }
    }

    private async Task<bool?> ReclaimCandidateAsync(SqliteConnection connection,
        AttachmentReclaimCandidate candidate, DateTimeOffset cutoff, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            if (candidate.LastWriteUtc >= cutoff || _leases?.IsLeased(candidate.Path) == true) return false;
            if (await AttachmentSnapshotReferences.IsReferencedAsync(connection, candidate, cancellationToken))
                return false;
            await AttachmentReclaimIntents.RecordAsync(connection, candidate.Path, candidate.Length, now, cancellationToken);
            var referenced = !AttachmentSnapshotPaths.TryProbeCandidate(candidate, cutoff)
                || _leases?.IsLeased(candidate.Path) == true
                || await AttachmentSnapshotReferences.IsReferencedAsync(connection, candidate, cancellationToken);
            if (referenced)
            {
                await AttachmentReclaimIntents.MarkAsync(connection, candidate.Path,
                    AttachmentReclaimStage.Retained, null, now, cancellationToken);
                return false;
            }
            AttachmentSnapshotPaths.DeleteByHandle(candidate.Path);
            await AttachmentReclaimIntents.MarkAsync(connection, candidate.Path,
                AttachmentReclaimStage.Reclaimed, null, now, cancellationToken);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or SqliteException)
        {
            await TryMarkFailureAsync(connection, candidate.Path, now, ErrorCode(exception));
            return null;
        }
    }

    private async Task TryMarkFailureAsync(SqliteConnection connection, string path, DateTimeOffset now, string error)
    {
        try
        {
            await AttachmentReclaimIntents.MarkAsync(connection, path, AttachmentReclaimStage.Failed,
                error, now, CancellationToken.None);
        }
        catch (Exception)
        {
            // Journal updates are best-effort; the next pass retries.
        }
    }

    private static string ErrorCode(Exception exception) => exception switch
    {
        UnauthorizedAccessException => "unauthorized",
        FileNotFoundException => "missing",
        SqliteException => "sqlite",
        InvalidDataException => "invalid",
        _ => "io"
    };

    private static void PruneEmptyDirectories(HashSet<string> directories)
    {
        foreach (var directory in directories)
        {
            try
            {
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
                // Non-empty or contested directories are left for the next pass.
            }
        }
    }
}
