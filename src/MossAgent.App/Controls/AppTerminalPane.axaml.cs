using Avalonia.Markup.Xaml;
using Avalonia;
using Avalonia.Controls;
using MossAgent.App.Terminal;

namespace MossAgent.App.Controls;

public sealed partial class AppTerminalPane : AppUserControl
{
    public AppTerminalPane() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs args)
    {
        base.OnAttachedToVisualTree(args);
        this.FindControl<AppTerminalViewport>("TerminalViewport")!.PropertyChanged += HandleViewportChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        this.FindControl<AppTerminalViewport>("TerminalViewport")!.PropertyChanged -= HandleViewportChanged;
        this.FindControl<AppIconButton>("TerminalMenuButton")!.Flyout?.Hide();
        base.OnDetachedFromVisualTree(args);
    }

    private void HandleViewportChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property == AppTerminalViewport.InputScopeVersionProperty || args.Property == IsVisibleProperty || args.Property == IsEnabledProperty)
            this.FindControl<AppIconButton>("TerminalMenuButton")!.Flyout?.Hide();
        if (args.Property == AppTerminalViewport.FrameProperty)
        {
            var previous = args.OldValue as TerminalFrame;
            var current = args.NewValue as TerminalFrame;
            if (previous?.ViewId != current?.ViewId || previous?.SessionId != current?.SessionId || previous?.AlternateScreen != current?.AlternateScreen)
                this.FindControl<AppIconButton>("TerminalMenuButton")!.Flyout?.Hide();
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs args)
    {
        base.OnPropertyChanged(args);
        if (args.Property == DataContextProperty || args.Property == IsVisibleProperty || args.Property == IsEnabledProperty)
            this.FindControl<AppIconButton>("TerminalMenuButton")?.Flyout?.Hide();
    }
}
