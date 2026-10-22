using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using MossAgent.App.Resources;
using MossAgent.App.Services;

namespace MossAgent.App.Controls;

public sealed partial class AppAttachmentChip : AppUserControl
{
    public static readonly StyledProperty<string> DisplayPathProperty =
        AvaloniaProperty.Register<AppAttachmentChip, string>(nameof(DisplayPath), string.Empty);
    public static readonly StyledProperty<string> SizeLabelProperty =
        AvaloniaProperty.Register<AppAttachmentChip, string>(nameof(SizeLabel), string.Empty);
    public static readonly StyledProperty<ICommand?> RemoveCommandProperty =
        AvaloniaProperty.Register<AppAttachmentChip, ICommand?>(nameof(RemoveCommand));
    public static readonly StyledProperty<ICommand?> OpenCommandProperty =
        AvaloniaProperty.Register<AppAttachmentChip, ICommand?>(nameof(OpenCommand));
    public static readonly DirectProperty<AppAttachmentChip, bool> HasRemoveActionProperty =
        AvaloniaProperty.RegisterDirect<AppAttachmentChip, bool>(nameof(HasRemoveAction), chip => chip.HasRemoveAction);
    public static readonly StyledProperty<AttachmentPreviewSource?> ThumbnailProperty =
        AvaloniaProperty.Register<AppAttachmentChip, AttachmentPreviewSource?>(nameof(Thumbnail));
    public static readonly DirectProperty<AppAttachmentChip, bool> HasThumbnailRequestProperty =
        AvaloniaProperty.RegisterDirect<AppAttachmentChip, bool>(nameof(HasThumbnailRequest), chip => chip.HasThumbnailRequest);
    public static readonly DirectProperty<AppAttachmentChip, bool> HasOpenActionProperty =
        AvaloniaProperty.RegisterDirect<AppAttachmentChip, bool>(nameof(HasOpenAction), chip => chip.HasOpenAction);

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

    public ICommand? OpenCommand
    {
        get => GetValue(OpenCommandProperty);
        set => SetValue(OpenCommandProperty, value);
    }

    public bool HasRemoveAction => RemoveCommand is not null;
    public bool HasThumbnailRequest => Thumbnail is not null;
    public bool HasOpenAction => OpenCommand is not null && Thumbnail is not null;

    public AttachmentPreviewSource? Thumbnail
    {
        get => GetValue(ThumbnailProperty);
        set => SetValue(ThumbnailProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == DisplayPathProperty || change.Property == SizeLabelProperty)
            AutomationProperties.SetName(this, AttachmentChipText.AccessibleName(DisplayPath, SizeLabel));
        if (change.Property == RemoveCommandProperty)
            RaisePropertyChanged(HasRemoveActionProperty, change.OldValue is not null, change.NewValue is not null);
        if (change.Property == ThumbnailProperty)
        {
            RaisePropertyChanged(HasThumbnailRequestProperty, change.OldValue is not null, change.NewValue is not null);
            RaisePropertyChanged(HasOpenActionProperty,
                change.OldValue is not null && OpenCommand is not null,
                change.NewValue is not null && OpenCommand is not null);
        }
        if (change.Property == OpenCommandProperty)
            RaisePropertyChanged(HasOpenActionProperty,
                Thumbnail is not null && change.OldValue is not null,
                Thumbnail is not null && change.NewValue is not null);
    }
}
