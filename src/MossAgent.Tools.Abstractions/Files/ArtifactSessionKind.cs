namespace MossAgent.Tools.Abstractions.Files;

/// <summary>Distinguishes journaled file sessions: task artifacts, bounded diff inputs,
/// and SDK-owned temp files outside the task artifact directory.</summary>
public enum ArtifactSessionKind
{
    Artifact = 0,
    DiffInput = 1,
    ExternalSdkTemp = 2,
}
