using System.Net;
using Avalonia.Headless;
using MossAgent.Application.SourceControl;
using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using MossAgent.Infrastructure.SourceControl;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SourceAccountSettingsLifetimeTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task LateSaveKeepsNewerDraftAndNeverSubmitsIt() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var probe = new SourceAccountRepositoryProbe(fixture.Accounts);
        var entered = Signal();
        var release = Signal();
        probe.AfterSaveAsync = async () => { entered.TrySetResult(); await release.Task; };
        using var model = CreateModel(fixture, probe);
        await model.Initialization;
        OpenDraft(model, "original");
        var saving = model.SaveCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(fixture.Token);
            Assert.False(model.SaveCommand.CanExecute(null));
            model.CloseFormCommand.Execute(null);
            OpenDraft(model, "newer draft");
        }
        finally { release.TrySetResult(); }
        await saving;
        Assert.True(model.Editor.IsOpen);
        Assert.Equal("newer draft", model.Editor.Name);
        Assert.Equal("synthetic-source-lifetime-credential", model.Editor.AccessToken);
        Assert.Equal("original", Assert.Single(model.Items).Name);
        Assert.Equal(SourceAccountText.Get("PreviousSaved"), model.Status);
        Assert.Equal(1, probe.SaveCalls);
        Assert.Equal(0, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task LateEditorReadCannotReplaceNewForm() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var model = fixture.Model;
        OpenDraft(model, "stored");
        await model.SaveCommand.ExecuteAsync(null);
        var probe = new SourceAccountRepositoryProbe(fixture.Accounts);
        var entered = Signal();
        var release = Signal();
        probe.AfterGetAsync = async () => { entered.TrySetResult(); await release.Task; };
        using var other = CreateModel(fixture, probe);
        await other.Initialization;
        var opening = other.OpenEditFormCommand.ExecuteAsync(Assert.Single(other.Items));
        try
        {
            await entered.Task.WaitAsync(fixture.Token);
            OpenDraft(other, "do not replace");
        }
        finally { release.TrySetResult(); }
        await opening;
        Assert.Equal("do not replace", other.Editor.Name);
        Assert.Equal(0, other.Editor.StoredRevision);
        Assert.True(other.Editor.IsOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task CancelledIdentityRequestCleansUpAndPreservesDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var model = fixture.Model;
        OpenDraft(model, "stored");
        await model.SaveCommand.ExecuteAsync(null);
        var entered = Signal();
        var cleaned = Signal();
        fixture.Transport.RespondAsync = async (_, token) =>
        {
            entered.TrySetResult();
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { cleaned.TrySetResult(); }
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        OpenDraft(model, "keep this input");
        var verifying = model.VerifyCommand.ExecuteAsync(Assert.Single(model.Items));
        await entered.Task.WaitAsync(fixture.Token);
        model.VerifyCommand.Cancel();
        await verifying;
        Assert.True(cleaned.Task.IsCompleted);
        Assert.Equal(1, fixture.Transport.DisposedHandlers);
        Assert.False(model.IsBusy);
        Assert.True(model.HasStatusError);
        Assert.Equal(SourceAccountText.Get("Cancelled"), model.Status);
        Assert.Equal("keep this input", model.Editor.Name);
        Assert.True(model.Editor.IsOpen);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task DisabledProxyStaysSelectedButCannotSilentlyBecomeDirect() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var proxy = new ProxyProfile(Guid.NewGuid(), "proxy", ProxyProtocol.Http, "127.0.0.1", 23456, null, null, false, true);
        await fixture.Configurations.SaveProxyAsync(proxy, fixture.Token);
        var model = fixture.Model;
        await model.ReloadProxiesAsync();
        OpenDraft(model, "must not save");
        model.Editor.SelectedProxy = Assert.Single(model.Editor.Proxies, option => option.Id == proxy.Id);
        await fixture.Configurations.SaveProxyAsync(proxy with { IsEnabled = false }, fixture.Token);
        await model.ReloadProxiesAsync();
        Assert.Equal(proxy.Id, model.Editor.SelectedProxy?.Id);
        await model.SaveCommand.ExecuteAsync(null);
        Assert.True(model.HasStatusError);
        Assert.Equal(SourceAccountText.Get("ProxyUnavailable"), model.Status);
        Assert.Empty(await fixture.Accounts.GetAllAsync(fixture.Token));
        Assert.Equal(0, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ClosingOwnerSuppressesLateCommittedSaveWithoutReplayingOnReopen() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var probe = new SourceAccountRepositoryProbe(fixture.Accounts);
        var entered = Signal();
        var release = Signal();
        probe.AfterSaveAsync = async () => { entered.TrySetResult(); await release.Task; };
        using var model = CreateModel(fixture, probe);
        await model.Initialization;
        OpenDraft(model, "committed before cancellation");
        var saving = model.SaveCommand.ExecuteAsync(null);
        try
        {
            await entered.Task.WaitAsync(fixture.Token);
            model.Dispose();
            Assert.Empty(model.Editor.AccessToken);
            Assert.False(model.Editor.IsOpen);
        }
        finally { release.TrySetResult(); }
        await saving;
        Assert.Empty(model.Items);
        using var reopened = CreateModel(fixture, probe);
        await reopened.Initialization;
        Assert.Equal("committed before cancellation", Assert.Single(reopened.Items).Name);
        Assert.Equal(1, probe.SaveCalls);
        Assert.Equal(0, fixture.Transport.RequestCount);
    }, TestContext.Current.CancellationToken);

    private static SourceAccountSettingsViewModel CreateModel(SourceAccountUiFixture fixture, ISourceAccountRepository accounts) =>
        new(accounts, fixture.Configurations, new SourceAccountConnections(accounts, new GitHubIdentityClient(fixture.Transport)));

    [Fact]
    public Task IdentityDeadlineWaitsForCleanupAndDoesNotRetry() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        using var model = new SourceAccountSettingsViewModel(fixture.Accounts, fixture.Configurations,
            new SourceAccountConnections(fixture.Accounts, new GitHubIdentityClient(fixture.Transport)), TimeSpan.FromMilliseconds(250));
        await model.Initialization;
        OpenDraft(model, "timeout fixture");
        await model.SaveCommand.ExecuteAsync(null);
        var cleaned = Signal();
        fixture.Transport.RespondAsync = async (_, token) =>
        {
            try { await Task.Delay(Timeout.InfiniteTimeSpan, token); }
            finally { cleaned.TrySetResult(); }
            return new HttpResponseMessage(HttpStatusCode.OK);
        };
        await model.VerifyCommand.ExecuteAsync(Assert.Single(model.Items));
        Assert.True(cleaned.Task.IsCompleted);
        Assert.Equal(1, fixture.Transport.DisposedHandlers);
        Assert.Equal(1, fixture.Transport.RequestCount);
        Assert.Equal(SourceAccountText.Get("TimedOut"), model.Status);
        Assert.True(model.HasStatusError);
        Assert.False(model.IsBusy);
    }, TestContext.Current.CancellationToken);

    [Fact]
    public Task ProductionProxyChangesRefreshChoicesWithoutDiscardingSourceDraft() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var main = fixture.Main;
        var model = fixture.Model;
        OpenDraft(model, "preserved source draft");
        var proxies = main.Proxies;
        await proxies.RefreshCommand.ExecuteAsync(null);
        proxies.OpenCreateFormCommand.Execute(null);
        proxies.Name = "source proxy";
        proxies.Host = "127.0.0.1";
        proxies.Port = 23456;
        await proxies.SaveCommand.ExecuteAsync(null);
        Assert.False(proxies.HasStatusError, proxies.Status);
        var proxy = Assert.Single(proxies.Items);
        model.Editor.SelectedProxy = Assert.Single(model.Editor.Proxies, option => option.Id == proxy.Id);
        proxies.OpenEditFormCommand.Execute(proxy);
        proxies.IsEnabled = false;
        await proxies.SaveCommand.ExecuteAsync(null);
        Assert.False(proxies.HasStatusError, proxies.Status);
        Assert.Equal(proxy.Id, model.Editor.SelectedProxy?.Id);
        Assert.Equal("preserved source draft", model.Editor.Name);
        Assert.Equal("synthetic-source-lifetime-credential", model.Editor.AccessToken);
        await model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(SourceAccountText.Get("ProxyUnavailable"), model.Status);
        Assert.Empty(await fixture.Accounts.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);

    private static void OpenDraft(SourceAccountSettingsViewModel model, string name)
    {
        model.OpenCreateFormCommand.Execute(null);
        model.Editor.Name = name;
        model.Editor.AccessToken = "synthetic-source-lifetime-credential";
    }

    private static TaskCompletionSource Signal() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    [Fact]
    public Task ReferencedProxyDeletionExplainsSourceAccountDependency() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var proxy = new ProxyProfile(Guid.NewGuid(), "referenced proxy", ProxyProtocol.Http, "127.0.0.1", 23456,
            null, null, false, true);
        await fixture.Configurations.SaveProxyAsync(proxy, fixture.Token);
        var account = new SourceAccount(Guid.NewGuid(), "source", new Uri("https://api.github.com/"),
            "synthetic-proxy-reference-credential", proxy.Id, true);
        await fixture.Accounts.SaveAsync(account, fixture.Token);
        var proxies = fixture.Main.Proxies;
        await proxies.RefreshCommand.ExecuteAsync(null);
        proxies.OpenEditFormCommand.Execute(Assert.Single(proxies.Items));
        await proxies.DeleteCommand.ExecuteAsync(null);
        Assert.True(proxies.HasStatusError);
        Assert.Contains("源代码账号", proxies.Status, StringComparison.Ordinal);
        Assert.Single(await fixture.Configurations.GetProxiesAsync(fixture.Token));
        Assert.Single(await fixture.Accounts.GetAllAsync(fixture.Token));
    }, TestContext.Current.CancellationToken);
}
