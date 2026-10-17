using System.Xml.Linq;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelDiscoverySafetyTests
{
    [Fact]
    public async Task DisposalBlocksDiscoveryEvenWhenCommandIsInvokedDirectly()
    {
        var repository = ConfigurationMutationFailureTests.Repository();
        using var providers = new ProviderSettingsViewModel(repository);
        var discovery = new FixedModelDiscoveryService([new("late-model", "Late model")]);
        using var models = new ModelSettingsViewModel(repository, discovery, providers);
        models.Dispose();

        await models.FetchModelsCommand.ExecuteAsync(null);

        Assert.Null(discovery.LastRequestedProvider);
        Assert.Single(repository.Store.Models);
        Assert.Empty(models.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.ProxyAuthenticationRequired)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task FetchModels_DoesNotExposeExceptionSecrets(HttpStatusCode? statusCode)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>()
                .InitializeAsync(TestContext.Current.CancellationToken);
            var repository = services.GetRequiredService<IConfigurationRepository>();
            var provider = new AiProvider(Guid.NewGuid(), "fixture", ProviderProtocol.OpenAiResponses,
                new Uri("https://fixture.example/v1/"), "fixture-secret", null, false, true,
                new Dictionary<string, string>());
            var model = new ModelProfile(Guid.NewGuid(), provider.Id, "model", "model",
                128_000, 8_192, "medium", null, null, false, false, false, true, "{}");
            await repository.SaveProviderAsync(provider, TestContext.Current.CancellationToken);
            await repository.SaveModelAsync(model, TestContext.Current.CancellationToken);
            var discovery = new DeferredModelDiscoveryService();
            var viewModel = new ModelSettingsViewModel(repository, discovery,
                services.GetRequiredService<ProviderSettingsViewModel>());
            await viewModel.RefreshProvidersCommand.ExecuteAsync(null);

            var fetch = viewModel.FetchModelsForProviderCommand.ExecuteAsync(
                Assert.Single(viewModel.ProviderGroups));
            await discovery.Started.WaitAsync(
                TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Contains("正在从", viewModel.Status);
            Assert.False(viewModel.HasStatusError);
            var secretMessage = $"代理认证失败 HTTP 407 Authorization: Bearer {provider.ApiKey} fixture-proxy-password";
            discovery.Fail(statusCode is { } httpStatus
                ? new HttpRequestException(secretMessage, null, httpStatus)
                : new InvalidOperationException(secretMessage));
            await fetch;

            if (statusCode == HttpStatusCode.ProxyAuthenticationRequired)
            {
                Assert.Contains("代理认证失败", viewModel.Status);
                Assert.Contains("用户名", viewModel.Status);
                Assert.Contains("密码", viewModel.Status);
            }
            else
            {
                Assert.Contains("检查提供商地址", viewModel.Status);
                Assert.DoesNotContain("代理认证失败", viewModel.Status);
            }
            Assert.True(viewModel.HasStatusError);
            Assert.DoesNotContain(provider.ApiKey, viewModel.Status);
            Assert.DoesNotContain("Authorization", viewModel.Status);
            Assert.DoesNotContain("fixture-proxy-password", viewModel.Status);
            if (statusCode is { } expectedStatus) Assert.Contains($"HTTP {(int)expectedStatus}", viewModel.Status);
            else Assert.DoesNotContain("HTTP", viewModel.Status);
            Assert.Equal(model, Assert.Single(await repository.GetModelsAsync(
                provider.Id, TestContext.Current.CancellationToken)));
        }
        finally
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ProviderPage_ShowsModelFailuresWithSharedErrorAppearance()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        var document = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Views", "ProviderAndModelSettingsPane.axaml"));
        var pageStatus = Assert.Single(document.Descendants(), element => element.Name.LocalName == "AppSettingsPageHeader");
        Assert.Equal("{Binding Models.Status}", pageStatus.Attribute("Status")?.Value);
        Assert.Equal("{Binding Models.HasStatusError}", pageStatus.Attribute("IsStatusError")?.Value);
        var header = XDocument.Load(Path.Combine(directory.FullName, "src", "MossAgent.App",
            "Controls", "AppSettingsPageHeader.axaml"));
        var status = Assert.Single(header.Descendants(), element => element.Name.LocalName == "AppText"
            && element.Attribute("Text")?.Value == "{Binding Status, ElementName=Header}");
        Assert.Equal("{Binding IsStatusError, ElementName=Header}", status.Attribute("Classes.status-error")?.Value);
        Assert.Equal("{Binding HasStatus, ElementName=Header}", status.Attribute("IsVisible")?.Value);
        var editorStatus = Assert.Single(document.Descendants(), element =>
            element.Name.LocalName == "AppText" && element.Attribute("Text")?.Value == "{Binding Models.EditorStatus}");
        Assert.Contains("status-error", editorStatus.Attribute("Classes")!.Value.Split(' '));
        Assert.Equal("{Binding Models.HasEditorStatus}", editorStatus.Attribute("IsVisible")?.Value);
    }
}
