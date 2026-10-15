using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class ConversationTabViewModel : ObservableObject
{
    private AgentTask _task;
    private bool _isSelected;

    public ConversationTabViewModel(AgentTask task) => _task = task;

    public AgentTask Task
    {
        get => _task;
        set
        {
            if (SetProperty(ref _task, value)) OnPropertyChanged(nameof(Title));
        }
    }

    public string Title => Task.Title;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}
