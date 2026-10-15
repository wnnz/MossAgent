namespace MossAgent.Tools.Abstractions;

public sealed record FileSnapshot(string? Sha256)
{
    public bool Exists => Sha256 is not null;
}
