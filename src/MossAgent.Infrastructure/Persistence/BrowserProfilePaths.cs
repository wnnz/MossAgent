using MossAgent.Application.Artifacts;

namespace MossAgent.Infrastructure.Persistence;

public sealed class BrowserProfilePaths(AppDataPaths paths) : IBrowserProfilePaths
{
    public string GetManagedProfileDirectory(string profileName) =>
        Path.Combine(paths.BrowserProfilesPath, profileName);
}

