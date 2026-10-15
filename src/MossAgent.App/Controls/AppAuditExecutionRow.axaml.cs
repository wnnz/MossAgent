using Avalonia;

namespace MossAgent.App.Controls;

public sealed partial class AppAuditExecutionRow : AppUserControl
{
    public static readonly StyledProperty<string?> ToolNameProperty =
        AvaloniaProperty.Register<AppAuditExecutionRow, string?>(nameof(ToolName));
    public static readonly StyledProperty<string?> OutcomeLabelProperty =
        AvaloniaProperty.Register<AppAuditExecutionRow, string?>(nameof(OutcomeLabel));
    public static readonly StyledProperty<string?> OutcomeKindProperty =
        AvaloniaProperty.Register<AppAuditExecutionRow, string?>(nameof(OutcomeKind), "pending");
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AppAuditExecutionRow, string?>(nameof(Detail));
    public static readonly StyledProperty<string?> MetadataProperty =
        AvaloniaProperty.Register<AppAuditExecutionRow, string?>(nameof(Metadata));
    public static readonly DirectProperty<AppAuditExecutionRow, bool> HasDetailProperty =
        AvaloniaProperty.RegisterDirect<AppAuditExecutionRow, bool>(nameof(HasDetail), row => row.HasDetail);
    private bool _hasDetail;

    public AppAuditExecutionRow()
    {
        Classes.Add("app-audit-execution-row");
        InitializeComponent();
        UpdateState();
    }

    public string? ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }
    public string? OutcomeLabel { get => GetValue(OutcomeLabelProperty); set => SetValue(OutcomeLabelProperty, value); }
    public string? OutcomeKind { get => GetValue(OutcomeKindProperty); set => SetValue(OutcomeKindProperty, value); }
    public string? Detail { get => GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public string? Metadata { get => GetValue(MetadataProperty); set => SetValue(MetadataProperty, value); }
    public bool HasDetail => _hasDetail;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == OutcomeKindProperty || change.Property == DetailProperty) UpdateState();
    }

    private void UpdateState()
    {
        foreach (var kind in new[] { "success", "pending", "cancelled", "denied", "failed" })
            Classes.Set(kind, OutcomeKind == kind || kind == "failed" && OutcomeKind is not ("success" or "pending" or "cancelled" or "denied"));
        SetAndRaise(HasDetailProperty, ref _hasDetail, !string.IsNullOrWhiteSpace(Detail));
    }
}
