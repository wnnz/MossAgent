using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ProviderModelGroupViewModel : ObservableObject
{
    private bool _isExpanded;

    public ProviderModelGroupViewModel(AiProvider provider)
    {
        Provider = provider;
        Models.CollectionChanged += HandleModelsChanged;
    }

    public AiProvider Provider { get; }
    public ObservableCollection<ModelProfile> Models { get; } = [];
    public int ModelCount => Models.Count;
    public bool HasNoModels => Models.Count == 0;
    public bool IsProviderDisabled => !Provider.IsEnabled;
    public string ExpansionActionLabel => IsExpanded
        ? $"收起 {Provider.Name} 的模型"
        : $"展开 {Provider.Name} 的模型";

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                OnPropertyChanged(nameof(ExpansionActionLabel));
            }
        }
    }

    private void HandleModelsChanged(object? sender, NotifyCollectionChangedEventArgs eventArgs)
    {
        OnPropertyChanged(nameof(ModelCount));
        OnPropertyChanged(nameof(HasNoModels));
    }
}
