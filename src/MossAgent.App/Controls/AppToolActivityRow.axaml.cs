using System.Windows.Input;
using Avalonia;

namespace MossAgent.App.Controls;

public sealed partial class AppToolActivityRow : AppUserControl
{
    public static readonly StyledProperty<string> SummaryProperty =
        AvaloniaProperty.Register<AppToolActivityRow, string>(nameof(Summary), string.Empty);
    public static readonly StyledProperty<string> StatusProperty =
        AvaloniaProperty.Register<AppToolActivityRow, string>(nameof(Status), string.Empty);
    public static readonly StyledProperty<string> DescriptionProperty =
        AvaloniaProperty.Register<AppToolActivityRow, string>(nameof(Description), string.Empty);
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<AppToolActivityRow, bool>(nameof(IsExpanded));
    public static readonly StyledProperty<bool> CanExpandProperty =
        AvaloniaProperty.Register<AppToolActivityRow, bool>(nameof(CanExpand));
    public static readonly StyledProperty<bool> IsSubagentProperty =
        AvaloniaProperty.Register<AppToolActivityRow, bool>(nameof(IsSubagent));
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<AppToolActivityRow, ICommand?>(nameof(Command));

    public AppToolActivityRow() => InitializeComponent();

    public string Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public string Status
    {
        get => GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string Description
    {
        get => GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public bool IsExpanded
    {
        get => GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public bool CanExpand
    {
        get => GetValue(CanExpandProperty);
        set => SetValue(CanExpandProperty, value);
    }

    public bool IsSubagent
    {
        get => GetValue(IsSubagentProperty);
        set => SetValue(IsSubagentProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}
