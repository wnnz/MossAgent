using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Input;
using MossAgent.App.ViewModels;

using MossAgent.App.Controls;

namespace MossAgent.App.Views;

/// <summary>
/// 工作台主任务会话视图，集成顶部模型状态条、中间任务对话流与底部 Composer 输入卡片。
/// </summary>
public partial class WorkspaceChatView : AppUserControl
{
    private readonly ChatScrollFollowState _scrollFollow = new();
    private WorkspaceViewModel? _scrollWorkspace;
    private bool _scrollScheduled;

    public WorkspaceChatView()
    {
        CopyCodeCommand = new AsyncRelayCommand<string>(CopyCodeAsync);
        OpenAttachmentCommand = new AsyncRelayCommand<ChatMessageAttachmentViewModel>(OpenAttachmentAsync);
        InitializeComponent();
        Loaded += HandleLoaded;
        Unloaded += HandleUnloaded;
        DataContextChanged += HandleDataContextChanged;
    }

    public ICommand CopyCodeCommand { get; }
    public ICommand OpenAttachmentCommand { get; }

    private void HandleLoaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        MessageScrollViewer.ScrollChanged += HandleMessageScrollChanged;
        SizeChanged += HandleViewportSizeChanged;
        BindWorkspace();
        UpdateViewportWidth(Bounds.Width);
    }

    private void HandleUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs args)
    {
        MessageScrollViewer.ScrollChanged -= HandleMessageScrollChanged;
        SizeChanged -= HandleViewportSizeChanged;
        if (_scrollWorkspace is not null)
            _scrollWorkspace.Catalog.SelectionChanged -= HandleTaskSelectionChanged;
        _scrollWorkspace = null;
        _scrollScheduled = false;
    }

    private void HandleDataContextChanged(object? sender, EventArgs args)
    {
        if (IsLoaded) BindWorkspace();
    }

    private void HandleViewportSizeChanged(object? sender, SizeChangedEventArgs args) =>
        UpdateViewportWidth(args.NewSize.Width);

    private void UpdateViewportWidth(double width)
    {
        if (TopLevel.GetTopLevel(this)?.DataContext is MainWindowViewModel viewModel)
            viewModel.SetChatViewportWidth(width);
    }

    private void BindWorkspace()
    {
        if (_scrollWorkspace is not null)
            _scrollWorkspace.Catalog.SelectionChanged -= HandleTaskSelectionChanged;
        _scrollWorkspace = DataContext as WorkspaceViewModel;
        if (_scrollWorkspace is not null)
            _scrollWorkspace.Catalog.SelectionChanged += HandleTaskSelectionChanged;
        HandleTaskSelectionChanged();
    }

    private void HandleTaskSelectionChanged()
    {
        _scrollFollow.Reset();
        ScheduleScrollToEnd();
    }

    private void HandleMessageScrollChanged(object? sender, ScrollChangedEventArgs args)
    {
        var viewer = MessageScrollViewer;
        var distance = Math.Max(0, viewer.Extent.Height - viewer.Viewport.Height - viewer.Offset.Y);
        if (_scrollFollow.Update(args.ExtentDelta.Y, args.ViewportDelta.Y,
                args.OffsetDelta.Y, distance))
            ScheduleScrollToEnd();
    }

    private void ScheduleScrollToEnd()
    {
        if (_scrollScheduled || _scrollWorkspace is null) return;
        _scrollScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _scrollScheduled = false;
            if (_scrollWorkspace is not null && _scrollFollow.IsFollowing)
                MessageScrollViewer.ScrollToEnd();
        }, DispatcherPriority.Background);
    }

    private async Task CopyCodeAsync(string? code)
    {
        if (code is not null) await CopyTextAsync(code);
    }

    private Task OpenAttachmentAsync(ChatMessageAttachmentViewModel? item, CancellationToken token) =>
        (DataContext as WorkspaceViewModel)?.FilePreview.OpenAttachmentAsync(item, token) ?? Task.CompletedTask;

    private async Task CopyTextAsync(string text)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is not null) await clipboard.SetTextAsync(text);
    }
}
