namespace MossAgent.Infrastructure.Tests;

internal sealed record WorktreeCreationIntentRow(Guid ProjectId, string Repository, string BaseCommit,
    string Target, string? VolumeId, string? FileId, int State, string? ClaimedAt);
