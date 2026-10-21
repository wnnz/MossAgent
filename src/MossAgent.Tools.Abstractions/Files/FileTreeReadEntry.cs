namespace MossAgent.Tools.Abstractions.Files;

// The root has an empty relative path. A null stream denotes a directory.
public sealed record FileTreeReadEntry(string RelativePath, FileStream? Stream);
