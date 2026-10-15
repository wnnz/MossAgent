using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ProxySettingsRowCommandTests
{
    [Fact]
    public async Task RowCommandsTargetClickedProxyInsteadOfPreviousSelection()
    {
        await WithFixture(async (viewModel, tester) =>
        {
            var first = viewModel.Items[0];
            var second = viewModel.Items[1];
            viewModel.SelectedItem = first;

            Assert.True(viewModel.TestCommand.CanExecute(second));
            await viewModel.TestCommand.ExecuteAsync(second);
            Assert.Same(second, Assert.Single(tester.Tested));
            Assert.Same(first, viewModel.SelectedItem);

            viewModel.OpenEditFormCommand.Execute(second);
            Assert.True(viewModel.IsFormOpen);
            Assert.Same(second, viewModel.SelectedItem);
            Assert.Equal(second.Name, viewModel.Name);
            Assert.Equal(second.Port, viewModel.Port);
            Assert.Equal(second.Password, viewModel.Password);
            viewModel.CloseFormCommand.Execute(null);
            viewModel.OpenEditFormCommand.Execute(first);
            Assert.Same(first, viewModel.SelectedItem);
        });
    }

    [Fact]
    public async Task UnknownRemovedAndDisabledTargetsCannotBeTested()
    {
        await WithFixture(async (viewModel, tester) =>
        {
            var proxy = viewModel.Items[0];
            viewModel.SelectedItem = proxy;
            var unknown = proxy with { Id = Guid.NewGuid() };
            Assert.False(viewModel.TestCommand.CanExecute(unknown));
            await viewModel.TestCommand.ExecuteAsync(unknown);
            viewModel.OpenEditFormCommand.Execute(unknown);
            Assert.False(viewModel.IsFormOpen);

            viewModel.Items.Remove(proxy);
            Assert.False(viewModel.OpenEditFormCommand.CanExecute(proxy));
            viewModel.OpenEditFormCommand.Execute(proxy);
            await viewModel.TestCommand.ExecuteAsync(proxy);
            Assert.False(viewModel.IsFormOpen);
            var disabled = viewModel.Items[0] with { IsEnabled = false };
            viewModel.Items[0] = disabled;
            Assert.False(viewModel.TestCommand.CanExecute(disabled with { IsEnabled = true }));
            await viewModel.TestCommand.ExecuteAsync(disabled);
            Assert.Empty(tester.Tested);
        });
    }

    [Fact]
    public async Task StaleRecordsResolveToCurrentSavedConfiguration()
    {
        await WithFixture(async (viewModel, tester) =>
        {
            var saved = viewModel.Items[1];
            var supplied = saved with { Host = "untrusted.test", Port = 9999, Password = "different" };
            await viewModel.TestCommand.ExecuteAsync(supplied);
            Assert.Same(saved, Assert.Single(tester.Tested));
            viewModel.OpenEditFormCommand.Execute(supplied);
            Assert.Equal(saved.Host, viewModel.Host);
            Assert.Equal(saved.Password, viewModel.Password);
        });
    }

    [Fact]
    public async Task CancellationReachesTesterAndUnexpectedErrorsDoNotExposeSecrets()
    {
        await WithFixture(async (viewModel, tester) =>
        {
            tester.BeforeResult = cancellationToken => Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            var pending = viewModel.TestCommand.ExecuteAsync(viewModel.Items[0]);
            viewModel.TestCommand.Cancel();
            await pending;
            Assert.Contains("取消", viewModel.Status);
            Assert.False(viewModel.TestCommand.IsRunning);

            tester.BeforeResult = _ => throw new InvalidOperationException("credential-must-not-appear");
            await viewModel.TestCommand.ExecuteAsync(viewModel.Items[1]);
            Assert.Contains("失败", viewModel.Status);
            Assert.DoesNotContain("credential-must-not-appear", viewModel.Status);
        });
    }

    private static async Task WithFixture(Func<ProxySettingsViewModel, RecordingProxyConnectionTester, Task> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(TestContext.Current.CancellationToken);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            foreach (var name in new[] { "Proxy A", "Proxy B" })
            {
                await repository.SaveProxyAsync(new ProxyProfile(Guid.NewGuid(), name, ProxyProtocol.Socks5,
                    "127.0.0.1", 1080, "user", "fixture-password", false, true), TestContext.Current.CancellationToken);
            }
            var tester = new RecordingProxyConnectionTester();
            var viewModel = new ProxySettingsViewModel(repository, tester);
            await viewModel.RefreshCommand.ExecuteAsync(null);
            await test(viewModel, tester);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
