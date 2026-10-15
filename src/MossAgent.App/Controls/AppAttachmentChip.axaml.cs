using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using MossAgent.App.Resources;

namespace MossAgent.App.Controls;

public sealed partial class AppAttachmentChip : AppUserControl
{
    public static readonly StyledProperty<string> DisplayPathProperty =
        AvaloniaProperty.Register<AppAttachmentChip, string>(nameof(DisplayPath), string.Empty);
    public static readonly StyledProperty<string> SizeLabelProperty =
        AvaloniaProperty.Register<AppAttachmentChip, string>(nameof(SizeLabel), string.Empty);
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<AppAttachmentChip, ICommand?>(nameof(RemoveCommand));
    public static readonly DirectProperty<AppAttachmentChip, bool> HasRemoveActionProperty =
        AvaloniaProperty.RegisterDirect<AppAttachmentChip, bool>(nameof(HasRemoveAction), chip => chip.HasRemoveAction);

    public AppAttachmentChip() => InitializeComponent();

    public string DisplayPath
    {
        get => GetValue(DisplayPathProperty);
        set => SetValue(DisplayPathProperty, value);
    }

    public string SizeLabel
    {
        get => GetValue(SizeLabelProperty);
        set => SetValue(SizeLabelProperty, value);
    }

    public ICommand? RemoveCommand
    {
        get => GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    public bool HasRemoveAction => RemoveCommand is not null;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayPathProperty || change.Property == SizeLabelProperty)
            AutomationProperties.SetName(this, AttachmentChipText.AccessibleName(DisplayPath, SizeLabel));
        if (change.Property == RemoveCommandProperty)
            RaisePropertyChanged(HasRemoveActionProperty, change.OldValue is not null, change.NewValue is not null);
    }
}
