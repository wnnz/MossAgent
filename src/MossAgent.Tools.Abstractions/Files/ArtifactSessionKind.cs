namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Distinguishes journaled file sessions: task artifacts versus bounded diff inputs.</summary>
public enum ArtifactSessionKind
{
    Artifact = 0,
    DiffInput = 1,
}
