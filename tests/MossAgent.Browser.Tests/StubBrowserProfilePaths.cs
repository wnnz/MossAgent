using MossAgent.Application.Artifacts;

namespace MossAgent.Browser.Tests;

internal sealed class StubBrowserProfilePaths : IBrowserProfilePaths
{
    public string GetManagedProfileDirectory(string profileName) =>
        Path.Combine(Path.GetTempPath(), "MossAgent.Tests", profileName);
}
