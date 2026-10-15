using System.Collections.Immutable;

namespace MossAgent.Application.Models;

public sealed record ModelCapabilityData(
    int SchemaVersion,
    string Source,
    string SourceSha256,
    DateTimeOffset RetrievedAt,
    ImmutableArray<ModelCapabilities> Models);
