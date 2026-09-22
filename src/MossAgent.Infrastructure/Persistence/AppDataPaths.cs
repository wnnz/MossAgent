namespace MossAgent.Infrastructure.Persistence;

public sealed class AppDataPaths
{
    public AppDataPaths(string? root = null)
    {
        Root = root ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MossAgent");
    }

    public string Root { get; }
    public string DatabasePath => Path.Combine(Root, "mossagent.db");
    public string ArtifactsPath => Path.Combine(Root, "artifacts");
    public string BrowserProfilesPath => Path.Combine(Root, "browser-profiles");
    public string WorktreesPath => Path.Combine(Root, "worktrees");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(ArtifactsPath);
        Directory.CreateDirectory(BrowserProfilesPath);
        Directory.CreateDirectory(WorktreesPath);
    }
}

