using Avalonia.Headless;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class SourceAccountRevocationTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task UnavailableProxyDoesNotPreventAccountRevocationOrBecomeDirect() => _session.DispatchAsync(async () =>
    {
        await using var fixture = await SourceAccountUiFixture.CreateAsync();
        var proxy = new ProxyProfile(Guid.NewGuid(), "revocation proxy", ProxyProtocol.Http, "127.0.0.1", 23456,
            null, null, false, true);
        await fixture.Configurations.SaveProxyAsync(proxy, fixture.Token);
        await fixture.Accounts.SaveAsync(new(Guid.NewGuid(), "revocation account", new Uri("https://api.github.com/"),
            "synthetic-revocation-credential", proxy.Id, true), fixture.Token);
        await fixture.Configurations.SaveProxyAsync(proxy with { IsEnabled = false }, fixture.Token);
        var model = fixture.Model;
        await model.RefreshCommand.ExecuteAsync(null);
        await model.OpenEditFormCommand.ExecuteAsync(Assert.Single(model.Items));
        Assert.Equal(proxy.Id, model.Editor.SelectedProxy?.Id);
        model.Editor.IsEnabled = false;
        await model.SaveCommand.ExecuteAsync(null);
        Assert.False(model.HasStatusError, model.Status);
        var item = Assert.Single(model.Items);
        Assert.False(item.IsEnabled);
        Assert.False(model.VerifyCommand.CanExecute(item));
        var stored = Assert.Single(await fixture.Accounts.GetAllAsync(fixture.Token));
        Assert.False(stored.IsEnabled);
        Assert.Equal(proxy.Id, stored.ProxyId);
        Assert.Equal(0, fixture.Transport.RequestCount);
        await model.OpenEditFormCommand.ExecuteAsync(item);
        model.Editor.IsEnabled = true;
        await model.SaveCommand.ExecuteAsync(null);
        Assert.True(model.HasStatusError);
        Assert.False(Assert.Single(await fixture.Accounts.GetAllAsync(fixture.Token)).IsEnabled);
        Assert.Equal(stored.Revision, Assert.Single(await fixture.Accounts.GetAllAsync(fixture.Token)).Revision);
    }, TestContext.Current.CancellationToken);
}
