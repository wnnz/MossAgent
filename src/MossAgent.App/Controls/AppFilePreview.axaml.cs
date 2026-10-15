using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.ComponentModel;
using Avalonia.Input;
using Avalonia.Input.Platform;
using MossAgent.App.ViewModels;

namespace MossAgent.App.Controls;

public sealed partial class AppFilePreview : AppUserControl
{
    private INotifyPropertyChanged? _observedContext;
    private FilePreviewSearchViewModel? _observedSearch;
    private readonly List<Visual> _ancestors = [];
    private bool _attached;
    private bool _isToolbarCompact;
    public static readonly DirectProperty<AppFilePreview, bool> IsToolbarCompactProperty =
        AvaloniaProperty.RegisterDirect<AppFilePreview, bool>(nameof(IsToolbarCompact), preview => preview.IsToolbarCompact);
    public static readonly StyledProperty<ICommand?> ClosePaneCommandProperty =
        AvaloniaProperty.Register<AppFilePreview, ICommand?>(nameof(ClosePaneCommand));
    public static readonly StyledProperty<ICommand?> ExpansionCommandProperty =
        AvaloniaProperty.Register<AppFilePreview, ICommand?>(nameof(ExpansionCommand));
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AppFilePreview, bool>(nameof(IsExpanded));
    public static readonly StyledProperty<bool> IsExpansionRequestedProperty =
        AvaloniaProperty.Register<AppFilePreview, bool>(nameof(IsExpansionRequested));
    public AppFilePreview()
    {
        CopyActions = new FilePreviewCopyCommands(() => _attached && IsVisible && IsEnabled && IsEffectivelyVisible && IsEffectivelyEnabled
            && TopLevel.GetTopLevel(this)?.Clipboard is not null,
            text => TopLevel.GetTopLevel(this)!.Clipboard!.SetTextAsync(text));
        InitializeComponent();
        Classes.Add("app-file-preview");
        SizeChanged += HandleSizeChanged;
    }
    public FilePreviewCopyCommands CopyActions { get; }
    public bool IsToolbarCompact => _isToolbarCompact;
    public ICommand? ClosePaneCommand { get => GetValue(ClosePaneCommandProperty); set => SetValue(ClosePaneCommandProperty, value); }
    public ICommand? ExpansionCommand { get => GetValue(ExpansionCommandProperty); set => SetValue(ExpansionCommandProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsExpansionRequested { get => GetValue(IsExpansionRequestedProperty); set => SetValue(IsExpansionRequestedProperty, value); }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        _attached = true;
        CopyActions.PropertyChanged += HandleCopyChanged;
        foreach (var ancestor in this.GetVisualAncestors())
        {
            _ancestors.Add(ancestor);
            ancestor.PropertyChanged += HandleAncestorChanged;
        }
        ObserveContext();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        RestoreLayout();
        _attached = false;
        CopyActions.PropertyChanged -= HandleCopyChanged;
        foreach (var ancestor in _ancestors) ancestor.PropertyChanged -= HandleAncestorChanged;
        _ancestors.Clear();
        if (_observedContext is not null) _observedContext.PropertyChanged -= HandleContextChanged;
        _observedContext = null;
        CopyActions.Attach(null);
        CloseMenus();
        ObserveSearch(null);
        base.OnDetachedFromVisualTree(args);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsExpandedProperty) CloseMenus();
        if (_attached && ((change.Property == IsVisibleProperty && !IsVisible)
            || (change.Property == IsEnabledProperty && !IsEnabled) || change.Property == DataContextProperty)) RestoreLayout();
        if (change.Property == DataContextProperty && _attached) ObserveContext();
        if (_attached && (change.Property == IsVisibleProperty || change.Property == IsEnabledProperty))
        {
            if (!IsVisible || !IsEnabled) CloseMenus();
            RefreshAvailability();
        }
    }

    private void ObserveContext()
    {
        if (_observedContext is not null) _observedContext.PropertyChanged -= HandleContextChanged;
        _observedContext = DataContext as INotifyPropertyChanged;
        if (_observedContext is not null) _observedContext.PropertyChanged += HandleContextChanged;
        CopyActions.Attach(DataContext as FilePreviewPaneViewModel);
        CloseMenus();
        ObserveSearch((DataContext as FilePreviewPaneViewModel)?.Active?.Search);
        Dispatcher.UIThread.Post(BringActiveIntoView, DispatcherPriority.Loaded);
    }

    private void HandleContextChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is "Active" or "IsOpen" or "CanReveal") CloseMenus();
        if (args.PropertyName == "Active") ObserveSearch((DataContext as FilePreviewPaneViewModel)?.Active?.Search);
        if (args.PropertyName is "Active" or "IsOpen") Dispatcher.UIThread.Post(BringActiveIntoView, DispatcherPriority.Loaded);
    }

    private void ObserveSearch(FilePreviewSearchViewModel? search)
    {
        if (_observedSearch is not null) _observedSearch.PropertyChanged -= HandleSearchChanged;
        _observedSearch = search;
        if (search is not null) search.PropertyChanged += HandleSearchChanged;
        if (search?.IsOpen == true) Dispatcher.UIThread.Post(FocusSearch, DispatcherPriority.Loaded);
    }

    private void HandleSearchChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FilePreviewSearchViewModel.IsOpen) && _observedSearch?.IsOpen == true)
            Dispatcher.UIThread.Post(FocusSearch, DispatcherPriority.Loaded);
    }

    private void FocusSearch()
    {
        if (!_attached || !IsEffectivelyVisible || _observedSearch?.IsOpen != true) return;
        SearchInput.Focus();
        SearchInput.SelectAll();
    }

    protected override void OnKeyDown(KeyEventArgs args)
    {
        base.OnKeyDown(args);
        if (args.Handled || !_attached || !IsEffectivelyVisible) return;
        if (args.Key == Key.C && args.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && CopyActions.CopyPathCommand.CanExecute(null))
        { CopyActions.CopyPathCommand.Execute(null); args.Handled = true; return; }
        if (_observedSearch is not { } search) return;
        if (args.Key == Key.F && args.KeyModifiers == KeyModifiers.Control && search.ShowCommand.CanExecute(null))
        { search.ShowCommand.Execute(null); FocusSearch(); args.Handled = true; }
        else if (search.IsOpen && args.Key == Key.Escape)
        { search.CloseCommand.Execute(null); CopyDefaultButton.Focus(); args.Handled = true; }
        else if (search.IsOpen && args.Key == Key.Enter && args.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift)
        {
            var command = args.KeyModifiers == KeyModifiers.Shift ? search.PreviousCommand : search.NextCommand;
            if (command.CanExecute(null)) command.Execute(null);
            args.Handled = true;
        }
    }

    private void CloseMenus()
    {
        CopyMenuButton?.Flyout?.Hide();
        OpenMenuButton?.Flyout?.Hide();
        ImageViewButton?.Flyout?.Hide();
        NewTabButton?.Flyout?.Hide();
        MoreActionsButton?.Flyout?.Hide();
    }

    private void HandleSizeChanged(object? sender, SizeChangedEventArgs args)
    {
        CloseMenus();
        SetAndRaise(IsToolbarCompactProperty, ref _isToolbarCompact, args.NewSize.Width < 400);
        Dispatcher.UIThread.Post(BringActiveIntoView, DispatcherPriority.Loaded);
    }

    private void RestoreLayout()
    {
        if (IsExpansionRequested && ExpansionCommand?.CanExecute(null) == true) ExpansionCommand.Execute(null);
    }

    private void HandleAncestorChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == IsVisibleProperty || args.Property == IsEnabledProperty) RefreshAvailability();
    }

    private void RefreshAvailability()
    {
        if (!IsEffectivelyVisible || !IsEffectivelyEnabled)
        {
            RestoreLayout();
            CloseMenus();
        }
        CopyActions.RefreshAvailability();
    }

    private void HandleCopyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(FilePreviewCopyCommands.CanShowMenu) && !CopyActions.CanShowMenu)
            CopyMenuButton?.Flyout?.Hide();
    }

    private void BringActiveIntoView()
    {
        if (!_attached || !IsEffectivelyVisible) return;
        var selected = this.GetVisualDescendants().OfType<AppActionButton>().FirstOrDefault(button => button.Classes.Contains("selected"));
        selected?.GetVisualAncestors().OfType<AppBorder>().FirstOrDefault(border => border.Classes.Contains("file-tab-host"))?.BringIntoView();
    }
}
