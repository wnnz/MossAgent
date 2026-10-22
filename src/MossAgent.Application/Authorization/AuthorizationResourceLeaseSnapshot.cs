namespace MossAgent.Application.Authorization;

public sealed record AuthorizationResourceLeaseSnapshot(
    Guid LeaseId,
    Guid ProjectId,
    long Revision,
    string InstanceId,
    DateTimeOffset RegisteredAt,
    DateTimeOffset HeartbeatAt);
