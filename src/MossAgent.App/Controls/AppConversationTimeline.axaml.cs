using System.Collections.ObjectModel;
using Avalonia;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppConversationTimeline : AppUserControl
{
    private bool _attached;
    public static readonly StyledProperty<ObservableCollection<ChatMessageViewModel>?> MessagesProperty =
        AvaloniaProperty.Register<AppConversationTimeline, ObservableCollection<ChatMessageViewModel>?>(nameof(Messages));
    public static readonly StyledProperty<bool> IsRunningProperty =
        AvaloniaProperty.Register<AppConversationTimeline, bool>(nameof(IsRunning));

    public AppConversationTimeline() => InitializeComponent();
    public ConversationTimelineViewModel Timeline { get; } = new();
    public ObservableCollection<ChatMessageViewModel>? Messages { get => GetValue(MessagesProperty); set => SetValue(MessagesProperty, value); }
    public bool IsRunning { get => GetValue(IsRunningProperty); set => SetValue(IsRunningProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        Timeline.Bind(Messages, IsRunning);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _attached = false;
        Timeline.Detach();
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_attached && (change.Property == MessagesProperty || change.Property == IsRunningProperty))
            Timeline.Bind(Messages, IsRunning);
    }
}
