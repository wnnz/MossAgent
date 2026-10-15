using System.Windows.Input;
using Avalonia;
using MossAgent.App.Controls;

namespace MossAgent.App.Views;

public sealed partial class SubagentDialogueView : AppUserControl
{
    public static readonly StyledProperty<ICommand?> CopyCodeCommandProperty =
        AvaloniaProperty.Register<SubagentDialogueView, ICommand?>(nameof(CopyCodeCommand));

    public SubagentDialogueView() => InitializeComponent();

    public ICommand? CopyCodeCommand
    {
        get => GetValue(CopyCodeCommandProperty);
        set => SetValue(CopyCodeCommandProperty, value);
    }
}
