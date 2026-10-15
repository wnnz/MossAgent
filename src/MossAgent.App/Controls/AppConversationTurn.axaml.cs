using Avalonia;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppConversationTurn : AppUserControl
{
    public static readonly StyledProperty<ConversationTurnViewModel?> TurnProperty =
        AvaloniaProperty.Register<AppConversationTurn, ConversationTurnViewModel?>(nameof(Turn));

    public AppConversationTurn() => InitializeComponent();
    public ConversationTurnViewModel? Turn { get => GetValue(TurnProperty); set => SetValue(TurnProperty, value); }
}
