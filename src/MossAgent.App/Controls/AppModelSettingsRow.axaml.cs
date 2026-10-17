using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed partial class AppModelSettingsRow : AppUserControl
{
    public static readonly StyledProperty<ModelProfile?> ModelProperty =
        AvaloniaProperty.Register<AppModelSettingsRow, ModelProfile?>(nameof(Model));
    public static readonly StyledProperty<ICommand?> EditCommandProperty =
        AvaloniaProperty.Register<AppModelSettingsRow, ICommand?>(nameof(EditCommand));
    public static readonly StyledProperty<ICommand?> DeleteCommandProperty =
        AvaloniaProperty.Register<AppModelSettingsRow, ICommand?>(nameof(DeleteCommand));
    public static readonly DirectProperty<AppModelSettingsRow, string> ContextLabelProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, string>(nameof(ContextLabel), row => row.ContextLabel);
    public static readonly DirectProperty<AppModelSettingsRow, string> ReasoningLabelProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, string>(nameof(ReasoningLabel), row => row.ReasoningLabel);
    public static readonly DirectProperty<AppModelSettingsRow, string> EditActionLabelProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, string>(nameof(EditActionLabel), row => row.EditActionLabel);
    public static readonly DirectProperty<AppModelSettingsRow, string> DeleteActionLabelProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, string>(nameof(DeleteActionLabel), row => row.DeleteActionLabel);
    public static readonly DirectProperty<AppModelSettingsRow, bool> HasEditActionProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, bool>(nameof(HasEditAction), row => row.HasEditAction);
    public static readonly DirectProperty<AppModelSettingsRow, bool> HasDeleteActionProperty =
        AvaloniaProperty.RegisterDirect<AppModelSettingsRow, bool>(nameof(HasDeleteAction), row => row.HasDeleteAction);

    private bool _hasEditAction;
    private bool _hasDeleteAction;
    private bool _isStacked;

    public AppModelSettingsRow() => InitializeComponent();

    public ModelProfile? Model { get => GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public ICommand? EditCommand { get => GetValue(EditCommandProperty); set => SetValue(EditCommandProperty, value); }
    public ICommand? DeleteCommand { get => GetValue(DeleteCommandProperty); set => SetValue(DeleteCommandProperty, value); }
    public string ContextLabel => FormatContext(Model);
    public string ReasoningLabel => FormatReasoning(Model);
    public string EditActionLabel => ActionText("EditModel", Model);
    public string DeleteActionLabel => ActionText("DeleteModel", Model);
    public string EditLabel => ProviderSettingsText.Get("Edit");
    public string DeleteLabel => ProviderSettingsText.Get("Delete");
    public bool HasEditAction => _hasEditAction;
    public bool HasDeleteAction => _hasDeleteAction;

    protected override Size MeasureOverride(Size availableSize)
    {
        var width = Bounds.Width > 0 ? Math.Min(Bounds.Width, availableSize.Width) : availableSize.Width;
        var stacked = width < 480;
        if (_isStacked != stacked)
        {
            _isStacked = stacked;
            ReflowDetails(IdentityLayout, ModelIdText, stacked);
            ReflowDetails(CapabilitiesLayout, ReasoningText, stacked);
        }
        return base.MeasureOverride(availableSize);
    }

    private static void ReflowDetails(Grid layout, AppText secondary, bool stacked)
    {
        layout.Classes.Set("stacked", stacked);
        layout.ColumnDefinitions[1].Width = stacked ? new GridLength(0) : GridLength.Star;
        Grid.SetRow(secondary, stacked ? 1 : 0);
        Grid.SetColumn(secondary, stacked ? 0 : 1);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == BoundsProperty && change.GetOldValue<Rect>().Width != Bounds.Width)
            InvalidateMeasure();
        if (change.Property == ModelProperty)
        {
            var previous = change.GetOldValue<ModelProfile?>();
            RaisePropertyChanged(ContextLabelProperty, FormatContext(previous), ContextLabel);
            RaisePropertyChanged(ReasoningLabelProperty, FormatReasoning(previous), ReasoningLabel);
            RaisePropertyChanged(EditActionLabelProperty, ActionText("EditModel", previous), EditActionLabel);
            RaisePropertyChanged(DeleteActionLabelProperty, ActionText("DeleteModel", previous), DeleteActionLabel);
        }
        if (change.Property == ModelProperty || change.Property == EditCommandProperty || change.Property == DeleteCommandProperty)
        {
            SetAndRaise(HasEditActionProperty, ref _hasEditAction, Model is not null && EditCommand is not null);
            SetAndRaise(HasDeleteActionProperty, ref _hasDeleteAction, Model is not null && DeleteCommand is not null);
        }
    }

    private static string FormatContext(ModelProfile? model) =>
        model is null ? string.Empty : ProviderSettingsText.Format("ContextLength", model.ContextLength);

    private static string FormatReasoning(ModelProfile? model) =>
        model is null ? string.Empty : ProviderSettingsText.Format("ReasoningEffort", model.ReasoningEffort);

    private static string ActionText(string key, ModelProfile? model) =>
        ProviderSettingsText.Format(key, model?.DisplayName ?? string.Empty);
}
