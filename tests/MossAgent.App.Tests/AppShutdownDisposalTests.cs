using Avalonia.Headless;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class AppShutdownDisposalTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(Avalonia.Application));
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Fact]
    public Task WindowStaysVisibleUntilAsyncServiceDisposalCompletes() => _session.DispatchAsync(async () =>
    {
        var services = new ServiceCollection();
        services.AddSingleton<AsyncDisposalProbe>();
        var provider = services.BuildServiceProvider();
        var probe = provider.GetRequiredService<AsyncDisposalProbe>();
        var window = new AppWindow();
        window.Show();
        var state = new AppShutdownViewModel();
        var coordinator = new WindowCloseCoordinator(window, state,
            async () => await provider.DisposeAsync());
        try
        {
            window.Close();
            await WaitUntilAsync(() => probe.Started);
            Assert.True(window.IsVisible);
            Assert.True(state.IsPending);
            Assert.False(probe.Disposed);
            Assert.False(probe.DispatcherTouched);

            probe.Release();
            await WaitUntilAsync(() => !window.IsVisible);
            Assert.True(probe.DispatcherTouched);
            Assert.True(probe.Disposed);
        }
        finally
        {
            probe.Release();
            await provider.DisposeAsync();
            coordinator.Dispose();
            if (window.IsVisible) window.Close();
        }
    }, TestContext.Current.CancellationToken);

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        while (!predicate()) await Task.Delay(15, deadline.Token);
    }
}
