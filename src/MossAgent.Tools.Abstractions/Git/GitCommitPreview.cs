namespace MossAgent.Tools.Abstractions.Git;

public sealed record GitCommitPreview(string State, int StagedFiles, string? Head, string? Branch);
