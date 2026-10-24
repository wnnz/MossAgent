using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.App.Resources;
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
        ? UiText.T("收起 {0} 的模型", Provider.Name)
        : UiText.T("展开 {0} 的模型", Provider.Name);

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
