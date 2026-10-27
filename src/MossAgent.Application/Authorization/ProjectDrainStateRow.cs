namespace MossAgent.Application.Authorization;

/// <summary>One instance's last published drain state for a project on the shared board.
/// Rows only exist for non-Idle states; publishing Idle deletes the row.</summary>
public sealed record ProjectDrainStateRow(Guid ProjectId, Guid InstanceId,
    AuthorizationDrainState State, DateTimeOffset UpdatedAt);
