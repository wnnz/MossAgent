using System.Collections.Concurrent;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class TaskBrowserSessionManager(
    IBrowserSessionFactory sessions,
    IEmbeddedBrowserSessionFactory embeddedSessions,
    IBrowserConfigurationRepository browserConfigurations,
    IConfigurationRepository configurations,
    IBrowserProfilePaths profiles) : ITaskBrowserSessionManager
{
    private readonly ConcurrentDictionary<Guid, LazyBrowserSession> _sessions = [];

    public IBrowserSession GetLazySession(Guid taskId) =>
        _sessions.GetOrAdd(taskId, id => new LazyBrowserSession(token => CreateAsync(id, token)));

    public async Task ReleaseAsync(Guid taskId)
    {
        if (_sessions.TryRemove(taskId, out var session))
        {
            await session.DisposeAsync();
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var taskId in _sessions.Keys)
        {
            await ReleaseAsync(taskId);
        }
    }

    private async Task<IBrowserSession> CreateAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var configuration = await browserConfigurations.GetAsync(cancellationToken);
        var proxy = await GetProxyAsync(configuration.ProxyId, cancellationToken);
        var options = new BrowserLaunchOptions(
            ToEngine(configuration.DefaultBrowser),
            ToProfileMode(configuration.ProfilePreference),
            GetProfileDirectory(configuration, taskId),
            configuration.CdpEndpoint,
            proxy?.ToUri(), proxy?.Username, proxy?.Password);
        return options.Engine == BrowserEngine.Embedded
            ? await embeddedSessions.CreateAsync(options, cancellationToken)
            : await sessions.CreateAsync(options, cancellationToken);
    }

    private async Task<ProxyProfile?> GetProxyAsync(
        Guid? proxyId,
        CancellationToken cancellationToken)
    {
        if (proxyId is null)
        {
            return null;
        }

        var available = await configurations.GetProxiesAsync(cancellationToken);
        return available.SingleOrDefault(proxy => proxy.Id == proxyId && proxy.IsEnabled)
            ?? throw new InvalidOperationException("浏览器选择的代理不存在或已禁用。");
    }

    private string? GetProfileDirectory(BrowserConfiguration configuration, Guid taskId)
    {
        return configuration.ProfilePreference switch
        {
            BrowserProfilePreference.ExistingCdp => null,
            BrowserProfilePreference.DailyProfile => configuration.ProfileDirectory,
            _ => profiles.GetManagedProfileDirectory(taskId.ToString("N"))
        };
    }

    private static BrowserEngine ToEngine(DefaultBrowserKind browser) =>
        browser switch
        {
            DefaultBrowserKind.Embedded => BrowserEngine.Embedded,
            DefaultBrowserKind.Chrome => BrowserEngine.Chrome,
            _ => BrowserEngine.Edge
        };

    private static BrowserProfileMode ToProfileMode(BrowserProfilePreference profile) =>
        profile switch
        {
            BrowserProfilePreference.DailyProfile => BrowserProfileMode.DailyProfile,
            BrowserProfilePreference.ExistingCdp => BrowserProfileMode.ExistingCdp,
            _ => BrowserProfileMode.Managed
        };
}
