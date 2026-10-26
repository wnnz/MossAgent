using System.Collections;
using Avalonia;

namespace MossAgent.App.Controls;

public sealed partial class AppAuditBrowserSessionRow : AppUserControl
{
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, string?>(nameof(Title));
    public static readonly StyledProperty<string?> OutcomeLabelProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, string?>(nameof(OutcomeLabel));
    public static readonly StyledProperty<string?> OutcomeKindProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, string?>(nameof(OutcomeKind), "running");
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, string?>(nameof(Detail));
    public static readonly StyledProperty<string?> MetadataProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, string?>(nameof(Metadata));
    public static readonly StyledProperty<IEnumerable?> ActionsProperty =
        AvaloniaProperty.Register<AppAuditBrowserSessionRow, IEnumerable?>(nameof(Actions));
    public static readonly DirectProperty<AppAuditBrowserSessionRow, bool> HasDetailProperty =
        AvaloniaProperty.RegisterDirect<AppAuditBrowserSessionRow, bool>(nameof(HasDetail), row => row.HasDetail);
    public static readonly DirectProperty<AppAuditBrowserSessionRow, bool> HasActionsProperty =
        AvaloniaProperty.RegisterDirect<AppAuditBrowserSessionRow, bool>(nameof(HasActions), row => row.HasActions);
    private bool _hasDetail;
    private bool _hasActions;

    public AppAuditBrowserSessionRow()
    {
        Classes.Add("app-audit-browser-session-row");
        InitializeComponent();
        UpdateState();
    }

    public string? Title { get => GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string? OutcomeLabel { get => GetValue(OutcomeLabelProperty); set => SetValue(OutcomeLabelProperty, value); }
    public string? OutcomeKind { get => GetValue(OutcomeKindProperty); set => SetValue(OutcomeKindProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public string? Metadata { get => GetValue(MetadataProperty); set => SetValue(MetadataProperty, value); }
    public IEnumerable? Actions { get => GetValue(ActionsProperty); set => SetValue(ActionsProperty, value); }
    public bool HasDetail => _hasDetail;
    public bool HasActions => _hasActions;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OutcomeKindProperty || change.Property == DetailProperty
            || change.Property == ActionsProperty) UpdateState();
    }

    private void UpdateState()
    {
        foreach (var kind in new[] { "running", "success", "cancelled", "interrupted", "failed" })
            Classes.Set(kind, OutcomeKind == kind || kind == "failed" && OutcomeKind is not ("running" or "success" or "cancelled" or "interrupted"));
        SetAndRaise(HasDetailProperty, ref _hasDetail, !string.IsNullOrWhiteSpace(Detail));
        SetAndRaise(HasActionsProperty, ref _hasActions, Actions is not null && Actions.Cast<object>().Any());
    }
}
