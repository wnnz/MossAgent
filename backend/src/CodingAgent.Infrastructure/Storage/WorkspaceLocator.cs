using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;

namespace CodingAgent.Infrastructure.Storage;

/// <summary>工作区目录定位：settings.workspace_path（空则默认 dataDir/workspace）。</summary>
public class WorkspaceLocator(ISettingsRepository settings, string defaultWorkspaceRoot) : IWorkspaceLocator
{
    private readonly ISettingsRepository _settings = settings;
    private readonly string _default = defaultWorkspaceRoot;

    public string WorkspaceRoot
    {
        get
        {
            var configured = _settings.GetAsync(DbSeeder.WorkspacePathKey).GetAwaiter().GetResult();
            if (string.IsNullOrWhiteSpace(configured))
            {
                return _default;
            }
            return Path.GetFullPath(configured);
        }
    }
}
