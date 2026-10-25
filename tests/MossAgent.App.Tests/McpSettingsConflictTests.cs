using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsConflictTests
{
    [Fact]
    public async Task Save_AfterExternalChange_ReportsConflictWithoutOverwriting()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            var saved = await repository.SaveAsync(new McpServerProfile(Guid.NewGuid(),
                "server", McpTransportKind.Stdio, "missing-command", "[]", null, "{}", null, "{}",
                null, false), cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenEditFormCommand.Execute(saved);
            viewModel.Command = "edited-command";
            var external = await repository.SaveAsync(saved with { Command = "external-change" },
                cancellationToken);
            Assert.Equal(2, external.Revision);

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.True(viewModel.HasStatusError);
            Assert.Contains("已被其他操作修改", viewModel.Status);
            Assert.True(viewModel.IsFormOpen);
            Assert.Equal("edited-command", viewModel.Command);
            var persisted = Assert.Single(await repository.GetAllAsync(cancellationToken));
            Assert.Equal("external-change", persisted.Command);
            Assert.Equal(2, persisted.Revision);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_SequentialEditsInOneSession_ChainRevisionsWithoutConflict()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "sequential";
            viewModel.Command = "missing-command";

            await viewModel.SaveCommand.ExecuteAsync(null);
            Assert.Equal(1, Assert.Single(await repository.GetAllAsync(cancellationToken)).Revision);

            viewModel.Command = "second-command";
            await viewModel.SaveCommand.ExecuteAsync(null);

            var persisted = Assert.Single(await repository.GetAllAsync(cancellationToken));
            Assert.Equal("second-command", persisted.Command);
            Assert.Equal(2, persisted.Revision);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
