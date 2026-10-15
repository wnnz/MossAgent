using System.Collections;
using System.Windows.Input;
using Avalonia;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppResponseContent : AppUserControl
{
    public static readonly StyledProperty<IEnumerable?> BlocksProperty =
        AvaloniaProperty.Register<AppResponseContent, IEnumerable?>(nameof(Blocks));
    public static readonly StyledProperty<ICommand?> CopyCodeCommandProperty =
        AvaloniaProperty.Register<AppResponseContent, ICommand?>(nameof(CopyCodeCommand));
    public static readonly DirectProperty<AppResponseContent, bool> HasCopyCodeCommandProperty =
        AvaloniaProperty.RegisterDirect<AppResponseContent, bool>(nameof(HasCopyCodeCommand),
            control => control.HasCopyCodeCommand);

    private bool _hasCopyCodeCommand;

    public AppResponseContent()
    {
        Classes.Add("app-response-content");
        InitializeComponent();
    }

    public IEnumerable? Blocks
    {
        get => GetValue(BlocksProperty);
        set => SetValue(BlocksProperty, value);
    }

    public ICommand? CopyCodeCommand
    {
        get => GetValue(CopyCodeCommandProperty);
        set => SetValue(CopyCodeCommandProperty, value);
    }

    public bool HasCopyCodeCommand => _hasCopyCodeCommand;
    public string CopyCodeLabel => WorkspaceText.Get("CopyCodeAction");

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == CopyCodeCommandProperty)
            SetAndRaise(HasCopyCodeCommandProperty, ref _hasCopyCodeCommand, CopyCodeCommand is not null);
    }
}
