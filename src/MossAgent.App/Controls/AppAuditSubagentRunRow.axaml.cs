using Avalonia;

namespace MossAgent.App.Controls;

public sealed partial class AppAuditSubagentRunRow : AppUserControl
{
    public static readonly StyledProperty<string?> ProfileNameProperty =
        AvaloniaProperty.Register<AppAuditSubagentRunRow, string?>(nameof(ProfileName));
    public static readonly StyledProperty<string?> OutcomeLabelProperty =
        AvaloniaProperty.Register<AppAuditSubagentRunRow, string?>(nameof(OutcomeLabel));
    public static readonly StyledProperty<string?> OutcomeKindProperty =
        AvaloniaProperty.Register<AppAuditSubagentRunRow, string?>(nameof(OutcomeKind), "running");
    public static readonly StyledProperty<string?> DetailProperty =
        AvaloniaProperty.Register<AppAuditSubagentRunRow, string?>(nameof(Detail));
    public static readonly StyledProperty<string?> MetadataProperty =
        AvaloniaProperty.Register<AppAuditSubagentRunRow, string?>(nameof(Metadata));
    public static readonly DirectProperty<AppAuditSubagentRunRow, bool> HasDetailProperty =
        AvaloniaProperty.RegisterDirect<AppAuditSubagentRunRow, bool>(nameof(HasDetail), row => row.HasDetail);
    private bool _hasDetail;

    public AppAuditSubagentRunRow()
    {
        Classes.Add("app-audit-subagent-run-row");
        InitializeComponent();
        UpdateState();
    }

    public string? ProfileName { get => GetValue(ProfileNameProperty); set => SetValue(ProfileNameProperty, value); }
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
        foreach (var kind in new[] { "running", "success", "cancelled", "interrupted", "failed" })
            Classes.Set(kind, OutcomeKind == kind || kind == "failed" && OutcomeKind is not ("running" or "success" or "cancelled" or "interrupted"));
        SetAndRaise(HasDetailProperty, ref _hasDetail, !string.IsNullOrWhiteSpace(Detail));
    }
}
