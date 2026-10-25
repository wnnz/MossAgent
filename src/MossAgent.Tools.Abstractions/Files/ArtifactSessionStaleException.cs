namespace MossAgent.Tools.Abstractions.Files;

/// <summary>A conditional journal update matched no row, meaning the record changed underneath the caller.</summary>
public sealed class ArtifactSessionStaleException : IOException;
