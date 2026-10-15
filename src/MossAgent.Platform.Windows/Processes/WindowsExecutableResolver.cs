namespace MossAgent.Platform.Windows.Processes;

internal static class WindowsExecutableResolver
{
    public static string Resolve(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (Path.IsPathFullyQualified(fileName)) return Path.GetFullPath(fileName);
        if (fileName.Contains(Path.DirectorySeparatorChar) || fileName.Contains(Path.AltDirectorySeparatorChar))
            throw new ArgumentException("Executable must have an absolute path or a PATH name.", nameof(fileName));
        var executable = Path.HasExtension(fileName) ? fileName : fileName + ".exe";
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? string.Empty).Split(Path.PathSeparator))
        {
            var path = directory.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path)) continue;
            var candidate = Path.Combine(path, executable);
            if (File.Exists(candidate)) return Path.GetFullPath(candidate);
        }
        throw new FileNotFoundException("The command executable is unavailable on PATH.");
    }
}
