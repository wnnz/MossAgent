using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceFollowupRowViewModel : ObservableObject
{
    private string _editText;

    internal WorkspaceFollowupRowViewModel(ConversationFollowupQueue expected, ConversationFollowup item,
        Action<WorkspaceFollowupRowViewModel> edit, Func<int, Task> move, Func<Task> remove, Func<bool> canMutate, Func<bool> canEdit)
    {
        Expected = expected; Item = item; _editText = item.Prompt;
        var index = expected.Items.ToList().FindIndex(entry => entry.Id == item.Id);
        EditCommand = new RelayCommand(() => edit(this), canEdit);
        MoveUpCommand = new AsyncRelayCommand(() => move(-1), () => canMutate() && index > 0);
        MoveDownCommand = new AsyncRelayCommand(() => move(1), () => canMutate() && index + 1 < expected.Items.Count);
        DeleteCommand = new AsyncRelayCommand(remove, canMutate);
    }

    internal ConversationFollowupQueue Expected { get; }
    internal ConversationFollowup Item { get; }
    public Guid Id => Item.Id;
    public string Prompt => Item.Prompt;
    public string ModelLabel => $"{Item.Model.DisplayName} · {Item.Model.ReasoningEffort}";
    public string AttachmentLabel => string.Format(WorkspaceText.Get("FollowupAttachmentCount"), Item.Attachments.Count);
    public bool HasAttachments => Item.Attachments.Count > 0;
    public string EditText { get => _editText; set => SetProperty(ref _editText, value); }
    public string EditLabel => WorkspaceText.Get("FollowupEdit");
    public string UpLabel => WorkspaceText.Get("FollowupUp");
    public string DownLabel => WorkspaceText.Get("FollowupDown");
    public string DeleteLabel => WorkspaceText.Get("FollowupDelete");
    public IRelayCommand EditCommand { get; }
    public IAsyncRelayCommand MoveUpCommand { get; }
    public IAsyncRelayCommand MoveDownCommand { get; }
    public IAsyncRelayCommand DeleteCommand { get; }
    internal void NotifyCommands()
    {
        EditCommand.NotifyCanExecuteChanged(); MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged(); DeleteCommand.NotifyCanExecuteChanged();
    }
}
