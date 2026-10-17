using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsViewModelTests
{
    [Fact]
    public void ServerRowActions_BindRowProfileAndCreateFormHidesDelete()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var view = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Views", "McpSettingsPane.axaml"));
        Assert.Equal("Auto,*", view.Descendants()
            .Single(element => element.Attribute("Classes")?.Value == "settings-content-column")
            .Attribute("RowDefinitions")?.Value);
        var row = Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppSettingsItemRow");
        Assert.Equal("{Binding}", row.Attribute("ActionTarget")?.Value);
        Assert.Equal("{Binding IsEnabled}", row.Attribute("IsPrimaryAlternate")?.Value);
        Assert.Equal("{x:Static labels:SettingsItemLabels.Enable}", row.Attribute("PrimaryLabel")?.Value);
        Assert.Equal("{x:Static labels:SettingsItemLabels.Disable}", row.Attribute("AlternatePrimaryLabel")?.Value);
        Assert.Contains("ToggleEnabledCommand", row.Attribute("PrimaryCommand")!.Value, StringComparison.Ordinal);
        Assert.Contains("OpenEditFormCommand", row.Attribute("SecondaryCommand")!.Value, StringComparison.Ordinal);
        Assert.DoesNotContain(view.Descendants(), element => element.Name.LocalName == "AppCheckBox"
            && element.Attribute("IsEnabled")?.Value == "False");

        var delete = Assert.Single(view.Descendants(), element =>
            element.Name.LocalName == "AppButton"
            && element.Attribute("Command")?.Value == "{Binding DeleteCommand}");
        Assert.Equal("{Binding CanDelete}", delete.Attribute("IsVisible")?.Value);
        Assert.Contains(view.Descendants(), element =>
            element.Name.LocalName == "AppSettingsPageHeader"
            && element.Attribute("Status")?.Value == "{Binding Status}");
    }

    [Fact]
    public async Task SaveAndEnable_ReportConnectionFailureWithoutClaimingSuccess()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "unavailable";
            viewModel.Command = $"mossagent-missing-{Guid.NewGuid():N}";

            await viewModel.SaveCommand.ExecuteAsync(null);

            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            var saved = Assert.Single(await repository.GetAllAsync(cancellationToken));
            Assert.True(saved.IsEnabled);
            Assert.Equal(saved.Id, viewModel.SelectedItem?.Id);
            Assert.True(viewModel.IsFormOpen);
            Assert.True(viewModel.HasStatus);
            Assert.Contains("已保存", viewModel.Status);
            Assert.Contains("连接失败", viewModel.Status);
            Assert.DoesNotContain("已启用并连接", viewModel.Status);

            await viewModel.ToggleEnabledCommand.ExecuteAsync(saved);
            Assert.Contains("已禁用", viewModel.Status);
            var disabled = Assert.Single(await repository.GetAllAsync(cancellationToken));
            await viewModel.ToggleEnabledCommand.ExecuteAsync(disabled);
            Assert.Contains("已启用，但连接失败", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task RowActions_UseTheirOwnServerAndCreateFormHidesDelete()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            var first = new McpServerProfile(
                Guid.NewGuid(), "first", McpTransportKind.Stdio,
                "missing-first", "[]", null, "{}", null, "{}", null, true);
            var second = first with
            {
                Id = Guid.NewGuid(),
                Name = "second",
                Command = "missing-second"
            };
            await repository.SaveAsync(first, cancellationToken);
            await repository.SaveAsync(second, cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.SelectedItem = second;

            await viewModel.ToggleEnabledCommand.ExecuteAsync(first);

            var profiles = await repository.GetAllAsync(cancellationToken);
            Assert.False(Assert.Single(profiles, item => item.Id == first.Id).IsEnabled);
            Assert.True(Assert.Single(profiles, item => item.Id == second.Id).IsEnabled);

            viewModel.OpenEditFormCommand.Execute(second);
            Assert.Equal(second.Id, viewModel.SelectedItem?.Id);
            Assert.Equal("second", viewModel.Name);
            Assert.True(viewModel.CanDelete);
            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.False(viewModel.CanDelete);
            Assert.True(viewModel.IsFormOpen);
            Assert.False(viewModel.HasStatus);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_RejectsDuplicateServerNameWithoutClosingForm()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(cancellationToken);
            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            await repository.SaveAsync(new McpServerProfile(
                Guid.NewGuid(), "existing", McpTransportKind.Stdio,
                "missing-command", "[]", null, "{}", null, "{}", null, false), cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "EXISTING";
            viewModel.Command = "another-command";

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.Contains("同名", viewModel.Status);
            Assert.True(viewModel.IsFormOpen);
            Assert.Single(await repository.GetAllAsync(cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Toggle_ReportsPersistenceFailureWithoutCrashing()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(cancellationToken);
            var connections = services.GetRequiredService<SqliteConnectionFactory>();
            await using (var connection = connections.Create())
            {
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = """
                    INSERT INTO mcp_servers(id,name,transport,command,arguments_json,
                        environment_json,headers_json,is_enabled)
                    VALUES($id,'legacy-id',0,'missing-command','[]','{}','{}',1);
                    """;
                command.Parameters.AddWithValue("$id", Guid.NewGuid().ToString("N"));
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            var repository = services.GetRequiredService<IMcpConfigurationRepository>();
            var profile = Assert.Single(await repository.GetAllAsync(cancellationToken));
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();

            await viewModel.ToggleEnabledCommand.ExecuteAsync(profile);

            Assert.Contains("无法切换", viewModel.Status);
            Assert.True(viewModel.HasStatus);
            Assert.True(Assert.Single(await repository.GetAllAsync(cancellationToken)).IsEnabled);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
