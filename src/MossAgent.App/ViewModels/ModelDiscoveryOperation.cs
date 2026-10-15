using System.Net;
using MossAgent.App.Resources;
using MossAgent.Application.Models;
using MossAgent.Application.Networking;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class ModelDiscoveryOperation(
    IConfigurationRepository repository,
    IModelDiscoveryService discovery,
    ProviderModelCatalogViewModel catalog,
    TimeSpan? discoveryTimeout)
{
    private readonly ModelDiscoveryImporter _importer = new(repository, catalog);
    private readonly TimeSpan _timeout = discoveryTimeout ?? TimeSpan.FromSeconds(30);

    public async Task ExecuteAsync(AiProvider provider, Func<AiProvider, DiscoveredModel, ModelProfile> createModel,
        CancellationToken ownerToken, Func<Task> refreshCatalog, Action<string, bool> reportStatus)
    {
        if (ownerToken.IsCancellationRequested || !IsTargetAvailable(provider)) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ownerToken);
        timeout.CancelAfter(_timeout);
        reportStatus(ProviderSettingsText.Format("ModelDiscoveryLoading", provider.Name), false);
        try
        {
            var discovered = await discovery.DiscoverAsync(provider, timeout.Token).WaitAsync(timeout.Token);
            timeout.Token.ThrowIfCancellationRequested();
            if (!await ImportAsync(provider, discovered, createModel, timeout.Token, reportStatus)) return;
            await refreshCatalog();
            timeout.Token.ThrowIfCancellationRequested();
            reportStatus(ProviderSettingsText.Format("ModelDiscoveryCompleted", provider.Name, discovered.Count), false);
        }
        catch (OperationCanceledException) when (ownerToken.IsCancellationRequested)
        {
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            reportStatus(ProviderSettingsText.Format("ModelDiscoveryTimedOut", provider.Name), true);
        }
        catch (Exception exception)
        {
            if (!ownerToken.IsCancellationRequested) reportStatus(FailureMessage(provider, exception), true);
        }
    }

    private async Task<bool> ImportAsync(AiProvider provider, IReadOnlyList<DiscoveredModel> discovered,
        Func<AiProvider, DiscoveredModel, ModelProfile> createModel, CancellationToken token, Action<string, bool> reportStatus)
    {
        if (!IsTargetAvailable(provider))
        {
            reportStatus(ProviderSettingsText.Get("DiscoveryTargetUnavailable"), true);
            return false;
        }
        var failed = false;
        if (await _importer.ImportAsync(provider, discovered, createModel, token, message =>
            {
                failed = true;
                reportStatus(message, true);
            })) return true;
        token.ThrowIfCancellationRequested();
        if (!failed) reportStatus(ProviderSettingsText.Get("ModelImportUnavailable"), true);
        return false;
    }

    private bool IsTargetAvailable(AiProvider provider) => catalog.IsAvailable && catalog.FindProvider(provider.Id) is not null;

    private static string FailureMessage(AiProvider provider, Exception exception) => exception switch
    {
        ProviderProxyUnavailableException => ProviderSettingsText.Format("ModelDiscoveryProxyUnavailable", provider.Name),
        HttpRequestException { StatusCode: HttpStatusCode.ProxyAuthenticationRequired } =>
            ProviderSettingsText.Format("ModelDiscoveryProxyAuthenticationFailed", provider.Name),
        HttpRequestException { StatusCode: { } statusCode } =>
            ProviderSettingsText.Format("ModelDiscoveryHttpFailed", provider.Name, (int)statusCode),
        _ => ProviderSettingsText.Format("ModelDiscoveryFailed", provider.Name)
    };
}
