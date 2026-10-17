using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsReloadTests
{
    [Fact]
    public async Task Reconnect_RetryReloadsServersAndProxyOptionsAfterInitialReadFailure()
    {
        using var fixture = new McpSettingsReloadFixture(initialFailure: true);
        var model = fixture.Model;
        Assert.Empty(model.Items);
        Assert.Contains("无法加载", model.Status);
        fixture.Repository.Reader = null;
        var server = McpSettingsReloadFixture.Server("recovered");
        fixture.Repository.Items = [server];
        var proxy = McpSettingsReloadFixture.Proxy("recovered-proxy");
        fixture.Proxies.Proxies.Add(proxy);

        await model.ReloadCommand.ExecuteAsync(null);

        Assert.Same(server, Assert.Single(model.Items));
        Assert.Contains(model.ProxyOptions, option => option.Id == proxy.Id);
        Assert.Equal(2, fixture.Repository.ReadCalls);
        Assert.Equal(1, fixture.Manager.ReloadCount);
        Assert.Contains("重新加载", model.Status);
        Assert.DoesNotContain("synthetic-private", model.Status);
        Assert.Equal(0, fixture.Repository.WriteCalls);
    }

    [Fact]
    public async Task Reconnect_ReadFailureKeepsOldSnapshotAndAllowsRecovery()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Repository.Reader = _ => Task.FromException<IReadOnlyList<McpServerProfile>>(
            new IOException("synthetic-private-read-error"));

        await fixture.Model.ReloadCommand.ExecuteAsync(null);

        Assert.Same(original, Assert.Single(fixture.Model.Items));
        Assert.Contains("无法", fixture.Model.Status);
        Assert.DoesNotContain("synthetic-private", fixture.Model.Status);
        fixture.Repository.Reader = null;
        var replacement = McpSettingsReloadFixture.Server("replacement");
        fixture.Repository.Items = [replacement];
        await fixture.Model.ReloadCommand.ExecuteAsync(null);
        Assert.Same(replacement, Assert.Single(fixture.Model.Items));
        Assert.Contains("重新加载", fixture.Model.Status);
        Assert.Equal(0, fixture.Repository.WriteCalls);
    }

    [Fact]
    public async Task Reconnect_RefreshesCatalogAndReportsConnectionFailureWithoutWritingSettings()
    {
        using var fixture = new McpSettingsReloadFixture();
        var replacement = McpSettingsReloadFixture.Server("disconnected");
        fixture.Repository.Items = [replacement];
        fixture.Manager.Errors = new Dictionary<string, string> { [replacement.Name] = "连接已断开" };

        await fixture.Model.ReloadCommand.ExecuteAsync(null);

        Assert.Same(replacement, Assert.Single(fixture.Model.Items));
        Assert.Contains("连接已断开", fixture.Model.Status);
        Assert.DoesNotContain("已启用并连接", fixture.Model.Status);
        Assert.Equal(0, fixture.Repository.WriteCalls);
    }

    [Fact]
    public async Task Reconnect_CancelDuringConnectionKeepsSnapshotAndRestoresActions()
    {
        using var fixture = new McpSettingsReloadFixture(blockReconnect: true);
        var model = fixture.Model;
        var original = Assert.Single(model.Items);
        model.SelectedItem = original;
        var run = model.ReloadCommand.ExecuteAsync(null);
        await fixture.Manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(model.CanEdit);
        Assert.False(model.OpenCreateFormCommand.CanExecute(null));
        Assert.False(model.OpenEditFormCommand.CanExecute(original));
        Assert.False(model.SaveCommand.CanExecute(null));
        Assert.False(model.ToggleEnabledCommand.CanExecute(original));
        Assert.False(model.DeleteCommand.CanExecute(null));

        model.CancelReloadCommand.Execute(null);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(fixture.Manager.WasCancelled);
        Assert.Same(original, Assert.Single(model.Items));
        Assert.Equal(1, fixture.Repository.ReadCalls);
        Assert.Contains("已取消", model.Status);
        Assert.True(model.CanEdit);
        Assert.True(model.OpenCreateFormCommand.CanExecute(null));
        Assert.True(model.DeleteCommand.CanExecute(null));
        fixture.Manager.Release.TrySetResult();
        await model.ReloadCommand.ExecuteAsync(null);
        Assert.Contains("重新加载", model.Status);
    }

    [Fact]
    public async Task Reconnect_CancelledReadCannotPublishLateSnapshot()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = fixture.Model.ReloadCommand.ExecuteAsync(null);
        Assert.Equal(2, fixture.Repository.ReadCalls);

        fixture.Model.CancelReloadCommand.Execute(null);
        pending.SetResult([McpSettingsReloadFixture.Server("late-result")]);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(fixture.Repository.ReadToken.IsCancellationRequested);
        Assert.Same(original, Assert.Single(fixture.Model.Items));
        Assert.Contains("已取消", fixture.Model.Status);
        Assert.True(fixture.Model.CanEdit);
    }

    [Fact]
    public async Task Dispose_CancelsReadAndSuppressesLateNotifications()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = fixture.Model.ReloadCommand.ExecuteAsync(null);
        var status = fixture.Model.Status;
        fixture.Model.Dispose();
        var notifications = new List<string?>();
        fixture.Model.PropertyChanged += (_, change) => notifications.Add(change.PropertyName);

        pending.SetResult([McpSettingsReloadFixture.Server("after-close")]);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.True(fixture.Repository.ReadToken.IsCancellationRequested);
        Assert.Same(original, Assert.Single(fixture.Model.Items));
        Assert.Equal(status, fixture.Model.Status);
        Assert.Empty(notifications);
        Assert.False(fixture.Model.ReloadCommand.CanExecute(null));
        Assert.False(fixture.Model.CanEdit);
    }

    [Fact]
    public async Task Reconnect_ProxyReadFailureDoesNotPartiallyPublishServerSnapshot()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        var direct = Assert.Single(fixture.Model.ProxyOptions);
        var replacement = McpSettingsReloadFixture.Server("replacement");
        fixture.Repository.Items = [replacement];
        fixture.Proxies.ProxyLoader = () => Task.FromException<IReadOnlyList<ProxyProfile>>(
            new IOException("synthetic-private-proxy-error"));

        await fixture.Model.ReloadCommand.ExecuteAsync(null);

        Assert.Same(original, Assert.Single(fixture.Model.Items));
        Assert.Same(direct, Assert.Single(fixture.Model.ProxyOptions));
        Assert.Contains("无法", fixture.Model.Status);
        Assert.DoesNotContain("synthetic-private", fixture.Model.Status);
        fixture.Proxies.ProxyLoader = null;
        await fixture.Model.ReloadCommand.ExecuteAsync(null);
        Assert.Same(replacement, Assert.Single(fixture.Model.Items));
    }

    [Fact]
    public async Task Reconnect_DoesNotRunOverAnOpenEditorDraft()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Model.OpenCreateFormCommand.Execute(null);
        fixture.Model.Name = "unsaved-draft";
        fixture.Model.HeadersJson = "{\"Authorization\":\"synthetic-private-header\"}";
        Assert.False(fixture.Model.ReloadCommand.CanExecute(null));

        await fixture.Model.ReloadCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.Equal(1, fixture.Repository.ReadCalls);
        Assert.Equal("unsaved-draft", fixture.Model.Name);
        Assert.Contains("synthetic-private-header", fixture.Model.HeadersJson);
        Assert.True(fixture.Model.IsFormOpen);
        fixture.Model.CloseFormCommand.Execute(null);
        Assert.True(fixture.Model.ReloadCommand.CanExecute(null));
    }
}
