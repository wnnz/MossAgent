using System.Xml.Linq;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsAdvancedFlowTests
{
    [Fact]
    public void Form_ExposesAllPersistedConnectionOptions()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var view = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Views", "McpSettingsPane.axaml"));
        foreach (var property in new[] { "ArgumentsJson", "EnvironmentJson", "HeadersJson" })
        {
            Assert.Contains(view.Descendants(), element => element.Name.LocalName == "AppInput"
                && element.Attribute("Text")?.Value == $"{{Binding {property}}}"
                && element.Attribute("Classes")?.Value.Split(' ').Contains("json-editor") == true);
        }

        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "AppComboBox"
            && element.Attribute("ItemsSource")?.Value == "{Binding ProxyOptions}"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedProxy}");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "AppComboBox"
            && element.Attribute("ItemsSource")?.Value == "{Binding TransportOptions}"
            && element.Attribute("SelectedItem")?.Value == "{Binding SelectedTransportOption}"
            && element.Attribute("DisplayMemberBinding")?.Value == "{Binding DisplayName}");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "AppSettingsItemRow"
            && element.Attribute("Subtitle")?.Value == "{Binding ., Converter={StaticResource McpServerSummaryConverter}}");
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "StackPanel"
            && element.Attribute("IsVisible")?.Value == "{Binding IsStdioTransport}"
            && element.Descendants().Any(child => child.Attribute("Text")?.Value == "{Binding EnvironmentJson}"));
        Assert.Contains(view.Descendants(), element => element.Name.LocalName == "StackPanel"
            && element.Attribute("IsVisible")?.Value == "{Binding IsHttpTransport}"
            && element.Descendants().Any(child => child.Attribute("Text")?.Value == "{Binding HeadersJson}"));
        var saveButton = Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppButton"
            && element.Attribute("Command")?.Value == "{Binding SaveCommand}");
        Assert.DoesNotContain(saveButton.Ancestors(), element => element.Name.LocalName == "AppScrollViewer");
        Assert.Contains(saveButton.Ancestors(), element => element.Name.LocalName == "AppSettingsDialog.Footer");
    }

    [Fact]
    public async Task Save_DisabledHttpServerPersistsHeadersAndProxyWithoutConnecting()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var configurations = services.GetRequiredService<IConfigurationRepository>();
            var proxy = new ProxyProfile(Guid.NewGuid(), "fixture-proxy", ProxyProtocol.Http,
                "127.0.0.1", 8080, null, null, false, true);
            await configurations.SaveProxyAsync(proxy, cancellationToken);

            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            Assert.True(viewModel.IsStdioTransport);
            viewModel.Name = "fixture-mcp";
            viewModel.SelectedTransportOption = McpTransportOption.FromTransport(McpTransportKind.StreamableHttp);
            Assert.True(viewModel.IsHttpTransport);
            Assert.False(viewModel.IsStdioTransport);
            viewModel.Url = "https://example.test/mcp";
            viewModel.HeadersJson = "{\"Authorization\":\"Bearer fixture-secret\"}";
            viewModel.EnvironmentJson = "{\"TOKEN\":\"fixture-secret\"}";
            viewModel.ArgumentsJson = "[\"--fixture\"]";
            viewModel.SelectedProxy = new ProxySelection(proxy.Id, proxy.Name);
            viewModel.IsEnabled = false;

            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(await services.GetRequiredService<IMcpConfigurationRepository>()
                .GetAllAsync(cancellationToken));
            Assert.Equal(proxy.Id, saved.ProxyId);
            Assert.Equal(viewModel.HeadersJson, saved.HeadersJson);
            Assert.Equal("{}", saved.EnvironmentJson);
            Assert.Equal("[]", saved.ArgumentsJson);
            Assert.False(saved.IsEnabled);
            Assert.DoesNotContain("fixture-secret", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_DisabledStdioServerPersistsEnvironmentButNotHttpOptions()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "stdio-fixture";
            viewModel.Command = "not-started";
            viewModel.ArgumentsJson = "[\"--fixture\"]";
            viewModel.EnvironmentJson = "{\"TOKEN\":\"fixture-secret\"}";
            viewModel.HeadersJson = "{\"Authorization\":\"Bearer fixture-secret\"}";
            viewModel.IsEnabled = false;

            await viewModel.SaveCommand.ExecuteAsync(null);

            var saved = Assert.Single(await services.GetRequiredService<IMcpConfigurationRepository>()
                .GetAllAsync(cancellationToken));
            Assert.Equal(viewModel.EnvironmentJson, saved.EnvironmentJson);
            Assert.Equal(viewModel.ArgumentsJson, saved.ArgumentsJson);
            Assert.Equal("{}", saved.HeadersJson);
            Assert.Null(saved.Url);
            Assert.Null(saved.ProxyId);
            Assert.False(saved.IsEnabled);
            Assert.DoesNotContain("fixture-secret", viewModel.Status);
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Save_RejectsMalformedSecretHeadersWithoutPersistingOrEchoingThem()
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            var cancellationToken = TestContext.Current.CancellationToken;
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
            var viewModel = services.GetRequiredService<McpSettingsViewModel>();
            viewModel.OpenCreateFormCommand.Execute(null);
            viewModel.Name = "rejected";
            viewModel.Transport = McpTransportKind.StreamableHttp;
            viewModel.Url = "https://example.test/mcp";
            viewModel.HeadersJson = "{\"Authorization\":\"private-bearer-token\"";

            await viewModel.SaveCommand.ExecuteAsync(null);

            Assert.True(viewModel.IsFormOpen);
            Assert.Contains("JSON 配置无效", viewModel.Status);
            Assert.DoesNotContain("private-bearer-token", viewModel.Status);
            Assert.Empty(await services.GetRequiredService<IMcpConfigurationRepository>()
                .GetAllAsync(cancellationToken));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
