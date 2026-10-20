using CommunityToolkit.Mvvm.ComponentModel;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed class WorkspaceSteeringReceiptViewModel(ConversationSteeringInput input) : ObservableObject
{
    private ConversationSteeringInput _input = input;
    private bool _isExpanded;
    public Guid Id => _input.Id;
    public string Text => _input.Text;
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }
    public string Status => WorkspaceText.Get(_input.State switch
    {
        ConversationSteeringInputState.Pending => "SteeringPending",
        ConversationSteeringInputState.AppliedAtBoundary => "SteeringApplied",
        _ => "SteeringInterrupted"
    });
    public string AcceptedAt => _input.AcceptedAt.ToLocalTime().ToString("g", System.Globalization.CultureInfo.CurrentCulture);
    public string Preview => Text.Length <= 160 ? Text : Text[..(char.IsHighSurrogate(Text[159]) ? 159 : 160)] + "…";
    internal void Update(ConversationSteeringInput value)
    {
        _input = value;
        OnPropertyChanged(nameof(Status));
    }
}
