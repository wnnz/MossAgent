using Avalonia;
using Avalonia.Controls;

namespace MossAgent.App.Controls;

public sealed class AppFileBreadcrumb : Panel
{
    public static readonly StyledProperty<string?> ParentPathProperty =
        AvaloniaProperty.Register<AppFileBreadcrumb, string?>(nameof(ParentPath));
    public static readonly StyledProperty<string?> FileNameProperty =
        AvaloniaProperty.Register<AppFileBreadcrumb, string?>(nameof(FileName));
    private readonly AppText _parentPath = new() { Classes = { "file-breadcrumb-parent" } };
    private readonly AppIcon _separator = new() { Classes = { "file-breadcrumb-separator" } };
    private readonly AppText _fileName = new() { Classes = { "file-breadcrumb-name" } };

    public AppFileBreadcrumb()
    {
        Classes.Add("app-file-breadcrumb");
        Children.Add(_parentPath);
        Children.Add(_separator);
        Children.Add(_fileName);
    }

    public string? ParentPath { get => GetValue(ParentPathProperty); set => SetValue(ParentPathProperty, value); }
    public string? FileName { get => GetValue(FileNameProperty); set => SetValue(FileNameProperty, value); }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (_parentPath is not null && change.Property == ParentPathProperty) _parentPath.Text = ParentPath;
        if (_fileName is not null && change.Property == FileNameProperty) _fileName.Text = FileName;
        if (change.Property == ParentPathProperty || change.Property == FileNameProperty) InvalidateMeasure();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _fileName.Measure(availableSize);
        var nameWidth = Math.Min(availableSize.Width, _fileName.DesiredSize.Width);
        var parentWidth = Math.Max(0, availableSize.Width - nameWidth - _separator.Size - 8);
        var showParent = !string.IsNullOrEmpty(ParentPath) && parentWidth >= 32;
        _parentPath.IsVisible = showParent;
        _separator.IsVisible = showParent;
        if (showParent)
        {
            _parentPath.Measure(new Size(parentWidth, availableSize.Height));
            _separator.Measure(availableSize);
        }
        var width = nameWidth + (showParent ? _parentPath.DesiredSize.Width + _separator.Size + 8 : 0);
        var height = Math.Max(_fileName.DesiredSize.Height, showParent ? _parentPath.DesiredSize.Height : 0);
        return new Size(Math.Min(availableSize.Width, width), height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var nameWidth = Math.Min(finalSize.Width, _fileName.DesiredSize.Width);
        var separatorWidth = _parentPath.IsVisible ? _separator.Size + 8 : 0;
        var parentWidth = _parentPath.IsVisible
            ? Math.Min(_parentPath.DesiredSize.Width, Math.Max(0, finalSize.Width - nameWidth - separatorWidth)) : 0;
        _parentPath.Arrange(new Rect(0, 0, parentWidth, finalSize.Height));
        _separator.Arrange(new Rect(parentWidth + 4, 0, Math.Max(0, separatorWidth - 8), finalSize.Height));
        _fileName.Arrange(new Rect(parentWidth + separatorWidth, 0, nameWidth, finalSize.Height));
        return finalSize;
    }
}
