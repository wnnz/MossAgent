using System.Collections.Concurrent;
using MossAgent.Application.Artifacts;
using MossAgent.Application.Persistence;
using MossAgent.Browser.Resources;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions.Browser;

namespace MossAgent.Browser;

public sealed class TaskBrowserSessionManager(
    IBrowserSessionFactory sessions,
    IEmbeddedBrowserSessionFactory embeddedSessions,
    IBrowserConfigurationRepository browserConfigurations,
    IConfigurationRepository configurations,
    IBrowserProfilePaths profiles,
    IBrowserProfileAccessApproval profileAccess) : ITaskBrowserSessionManager, IBrowserTakeoverController
{
    private readonly ConcurrentDictionary<Guid, LazyBrowserSession> _sessions = [];
    private readonly ConcurrentDictionary<Guid, TakeoverBrowserSession> _gatedSessions = [];
    private readonly ConcurrentDictionary<Guid, BrowserTaskSessionState> _states = [];

    public event EventHandler<BrowserControlStateChangedEventArgs>? StateChanged;

    public IBrowserSession GetLazySession(Guid taskId) =>
        _gatedSessions.GetOrAdd(taskId, id =>
            new TakeoverBrowserSession(
                GetOrCreateLazy(id), GetOrCreateState(id), () => MarkDisconnected(id)));

    public bool HasControlState(Guid taskId) =>
        (_sessions.TryGetValue(taskId, out var lazy) && lazy.CreatedSession is not null)
        || (_states.TryGetValue(taskId, out var state)
            && state.State != BrowserControlState.Automated);

    public BrowserControlState GetControlState(Guid taskId) =>
        _states.TryGetValue(taskId, out var state) ? state.State : BrowserControlState.Automated;

    public Task<BrowserControlState> BeginUserTakeoverAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var state = GetOrCreateState(taskId);
        var previous = state.State;
        var current = state.BeginUserTakeover();
        if (current != previous)
        {
            RaiseStateChanged(taskId, current);
        }

        return Task.FromResult(current);
    }

    public async Task<BrowserControlState> ReturnControlAsync(
        Guid taskId,
        CancellationToken cancellationToken)
    {
        var state = GetOrCreateState(taskId);
        if (!state.TryBeginReturn(out var generation, out var probeToken))
        {
            return state.State;
        }

        var previous = state.State;
        BrowserControlState current;
        try
        {
            RaiseStateChanged(taskId, BrowserControlState.ReturningToAutomated);
            var outcome = await ProbeInnerSessionAsync(taskId, probeToken, cancellationToken);
            current = state.CompleteReturn(generation, outcome);
        }
        catch (Exception)
        {
            current = state.CompleteReturn(generation, BrowserControlState.HandedToUser);
        }

        if (current != previous)
        {
            RaiseStateChanged(taskId, current);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return current;
    }

    public BrowserControlState MarkDisconnected(Guid taskId)
    {
        if (!_states.TryGetValue(taskId, out var state))
        {
            return BrowserControlState.Automated;
        }

        var previous = state.State;
        var current = state.MarkDisconnected();
        if (current != previous)
        {
            RaiseStateChanged(taskId, current);
        }

        return current;
    }

    public async Task ReleaseAsync(Guid taskId)
    {
        _gatedSessions.TryRemove(taskId, out _);
        if (_sessions.TryRemove(taskId, out var session))
        {
            await session.DisposeAsync();
        }

        if (_states.TryRemove(taskId, out var state))
        {
            state.Release();
            RaiseStateChanged(taskId, BrowserControlState.Automated);
        }
    }

    public async ValueTask DisposeAsync()
    {
        var taskIds = _sessions.Keys.Union(_states.Keys).ToArray();
        foreach (var taskId in taskIds)
        {
            await ReleaseAsync(taskId);
        }
    }

    private LazyBrowserSession GetOrCreateLazy(Guid taskId) =>
        _sessions.GetOrAdd(taskId, id =>
        {
            var approvals = new BrowserProfileApprovalCache();
            return new LazyBrowserSession(token => CreateAsync(id, approvals, token));
        });

    private BrowserTaskSessionState GetOrCreateState(Guid taskId) =>
        _states.GetOrAdd(taskId, _ => new BrowserTaskSessionState());

    private async Task<BrowserControlState> ProbeInnerSessionAsync(
        Guid taskId,
        CancellationToken probeToken,
        CancellationToken cancellationToken)
    {
        var active = _sessions.TryGetValue(taskId, out var lazy) ? lazy.CreatedSession : null;
        if (active is null)
        {
            return BrowserControlState.Automated;
        }

        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, probeToken);
            await active.ListTabsAsync(linked.Token);
            _ = active.CurrentUri;
            return BrowserControlState.Automated;
        }
        catch (ObjectDisposedException)
        {
            return BrowserControlState.Disconnected;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return BrowserControlState.HandedToUser;
        }
        catch (Exception)
        {
            return BrowserControlState.HandedToUser;
        }
    }

    private async Task<IBrowserSession> CreateAsync(
        Guid taskId,
        BrowserProfileApprovalCache approvals,
        CancellationToken cancellationToken)
    {
        var configuration = await browserConfigurations.GetAsync(cancellationToken);
        var proxy = await GetProxyAsync(configuration.ProxyId, cancellationToken);
        var options = new BrowserLaunchOptions(
            ToEngine(configuration.DefaultBrowser),
            ToProfileMode(configuration.ProfilePreference),
            GetProfileDirectory(configuration, taskId),
            configuration.CdpEndpoint,
            proxy?.ToUri(), proxy?.Username, proxy?.Password, taskId);
        await ConfirmProfileAccessAsync(taskId, options.ProfileMode, approvals, cancellationToken);
        return options.Engine == BrowserEngine.Embedded
            ? await embeddedSessions.CreateAsync(options, cancellationToken)
            : await sessions.CreateAsync(options, cancellationToken);
    }

    private async Task ConfirmProfileAccessAsync(
        Guid taskId,
        BrowserProfileMode mode,
        BrowserProfileApprovalCache approvals,
        CancellationToken cancellationToken)
    {
        if (mode == BrowserProfileMode.Managed || approvals.IsApproved(mode))
        {
            return;
        }

        if (!await profileAccess.ConfirmAsync(taskId, mode, cancellationToken))
        {
            throw new BrowserProfileAccessDeniedException(
                BrowserToolText.Get("BrowserProfileAccessDenied"));
        }

        approvals.Approve(mode);
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

    private void RaiseStateChanged(Guid taskId, BrowserControlState state) =>
        StateChanged?.Invoke(this, new BrowserControlStateChangedEventArgs(taskId, state));
}
