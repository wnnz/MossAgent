using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProviderModelCatalogViewModel : ObservableObject, IDisposable
{
    private readonly IConfigurationRepository _repository;
    private readonly SettingsOperationLifetime _operations;
    private int _loadVersion;
    private bool _isLoading;
    private bool _hasLoaded;
    private bool _hasLoadError;
    private bool _disposed;

    public ProviderModelCatalogViewModel(IConfigurationRepository repository, TimeSpan? readTimeout = null)
    {
        _repository = repository;
        _operations = new SettingsOperationLifetime(readTimeout);
        Mutations.PropertyChanged += HandleMutationStateChanged;
    }

    public ObservableCollection<AiProvider> Providers { get; } = [];
    public ObservableCollection<ProviderModelGroupViewModel> Groups { get; } = [];
    public SettingsMutationRunner Mutations { get; } = new();
    public event Action? SnapshotChanged;
    public CancellationToken LifetimeToken => _operations.Token;
    public bool IsLoading => _isLoading;
    public bool HasLoadError => _hasLoadError;
    public bool IsAvailable => !_disposed && _hasLoaded && !IsLoading && !HasLoadError;
    public bool CanMutate => IsAvailable && !Mutations.IsRunning;
    public bool IsEmpty => IsAvailable && Groups.Count == 0;
    public bool HasNotice => IsLoading || HasLoadError;
    public string Notice => IsLoading ? ProviderSettingsText.Get("CatalogLoading")
        : HasLoadError ? ProviderSettingsText.Get("CatalogReadFailed") : string.Empty;

    internal CancellationTokenSource CreateReadOperation() => _operations.CreateOperation(CancellationToken.None);

    internal AiProvider? FindProvider(Guid? id)
    {
        if (id is null) return null;
        for (var index = 0; index < Providers.Count; index++)
        {
            if (Providers[index].Id == id) return Providers[index];
        }
        return null;
    }

    internal ProviderModelGroupViewModel? FindGroup(Guid? providerId)
    {
        if (providerId is null) return null;
        for (var index = 0; index < Groups.Count; index++)
        {
            if (Groups[index].Provider.Id == providerId) return Groups[index];
        }
        return null;
    }

    internal ModelProfile? FindModel(Guid providerId, Guid modelId)
    {
        if (FindGroup(providerId) is not { } group) return null;
        for (var index = 0; index < group.Models.Count; index++)
        {
            if (group.Models[index].Id == modelId) return group.Models[index];
        }
        return null;
    }

    public async Task<bool> RefreshAsync()
    {
        if (_disposed) return false;
        var version = Interlocked.Increment(ref _loadVersion);
        _isLoading = true;
        _hasLoadError = false;
        NotifyState();
        try
        {
            using var timeout = CreateReadOperation();
            var providers = await _repository.GetProvidersAsync(timeout.Token).WaitAsync(timeout.Token);
            if (!IsCurrentRead(version, timeout.Token)) return false;
            var groups = new List<ProviderModelGroupViewModel>(providers.Count);
            foreach (var provider in providers)
            {
                var models = await _repository.GetModelsAsync(provider.Id, timeout.Token).WaitAsync(timeout.Token);
                if (!IsCurrentRead(version, timeout.Token)) return false;
                var group = new ProviderModelGroupViewModel(provider);
                group.Models.ReplaceWith(models.Where(model => model.ProviderId == provider.Id));
                groups.Add(group);
            }
            if (!IsCurrentRead(version, timeout.Token)) return false;
            Publish(providers, groups);
            return true;
        }
        catch (Exception)
        {
            if (IsCurrent(version)) _hasLoadError = true;
            return false;
        }
        finally
        {
            if (IsCurrent(version))
            {
                _isLoading = false;
                NotifyState();
            }
        }
    }

    private void Publish(IReadOnlyList<AiProvider> providers, List<ProviderModelGroupViewModel> groups)
    {
        var expanded = Groups.Where(group => group.IsExpanded).Select(group => group.Provider.Id).ToHashSet();
        foreach (var group in groups) group.IsExpanded = expanded.Contains(group.Provider.Id);
        Providers.ReplaceWith(providers);
        Groups.ReplaceWith(groups);
        _hasLoaded = true;
        _isLoading = false;
        SnapshotChanged?.Invoke();
    }

    private bool IsCurrent(int version) => !_disposed && version == Volatile.Read(ref _loadVersion);

    private bool IsCurrentRead(int version, CancellationToken token)
    {
        if (!IsCurrent(version)) return false;
        token.ThrowIfCancellationRequested();
        return true;
    }

    private void NotifyState()
    {
        foreach (var property in new[] { nameof(IsLoading), nameof(HasLoadError), nameof(IsAvailable),
            nameof(IsEmpty), nameof(HasNotice), nameof(Notice), nameof(CanMutate) }) OnPropertyChanged(property);
    }

    private void HandleMutationStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) => NotifyState();

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Mutations.PropertyChanged -= HandleMutationStateChanged;
        Interlocked.Increment(ref _loadVersion);
        _operations.Dispose();
        _isLoading = false;
        SnapshotChanged = null;
        NotifyState();
    }
}
