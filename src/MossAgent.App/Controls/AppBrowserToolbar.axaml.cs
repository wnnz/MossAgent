using System.Windows.Input;
using Avalonia;
using Avalonia.Data;

namespace MossAgent.App.Controls;

public sealed partial class AppBrowserToolbar : AppUserControl
{
    public static readonly StyledProperty<string> AddressProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, string>(nameof(Address), string.Empty,
            defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, ICommand?>(nameof(NavigateCommand));
    public static readonly StyledProperty<ICommand?> BackCommandProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, ICommand?>(nameof(BackCommand));
    public static readonly StyledProperty<ICommand?> ForwardCommandProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, ICommand?>(nameof(ForwardCommand));
    public static readonly StyledProperty<ICommand?> ReloadCommandProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, ICommand?>(nameof(ReloadCommand));
    public static readonly StyledProperty<ICommand?> DevToolsCommandProperty =
        AvaloniaProperty.Register<AppBrowserToolbar, ICommand?>(nameof(DevToolsCommand));

    public AppBrowserToolbar() => InitializeComponent();

    public string Address
    {
        get => GetValue(AddressProperty);
        set => SetValue(AddressProperty, value);
    }

    public ICommand? NavigateCommand
    {
        get => GetValue(NavigateCommandProperty);
        set => SetValue(NavigateCommandProperty, value);
    }

    public ICommand? BackCommand
    {
        get => GetValue(BackCommandProperty);
        set => SetValue(BackCommandProperty, value);
    }

    public ICommand? ForwardCommand
    {
        get => GetValue(ForwardCommandProperty);
        set => SetValue(ForwardCommandProperty, value);
    }

    public ICommand? ReloadCommand
    {
        get => GetValue(ReloadCommandProperty);
        set => SetValue(ReloadCommandProperty, value);
    }

    public ICommand? DevToolsCommand
    {
        get => GetValue(DevToolsCommandProperty);
        set => SetValue(DevToolsCommandProperty, value);
    }
}
