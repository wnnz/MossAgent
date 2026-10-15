using System.Windows.Input;
using Avalonia.Data.Converters;
using Avalonia.Data;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using MossAgent.App.Resources;
using MossAgent.Domain;

namespace MossAgent.App.Controls;

public sealed class AppApprovalPolicyButton : AppButton
{
    private readonly AppIconLabel _label;
    private readonly AppFlyout _flyout;
    private readonly Dictionary<ApprovalPolicy, AppButton> _options = [];

    public static readonly StyledProperty<ApprovalPolicy> PolicyProperty =
        AvaloniaProperty.Register<AppApprovalPolicyButton, ApprovalPolicy>(nameof(Policy), ApprovalPolicy.AskEveryTime);

    public static readonly StyledProperty<ICommand?> SelectPolicyCommandProperty =
        AvaloniaProperty.Register<AppApprovalPolicyButton, ICommand?>(nameof(SelectPolicyCommand));

    public AppApprovalPolicyButton()
    {
        Classes.Add("ghost");
        Classes.Add("app-approval-policy-button");
        _label = new AppIconLabel { Classes = { "policy-label", "action-content" } };
        Content = _label;
        ToolTip.SetTip(this, ApprovalPolicyText.Get("ChoosePolicy"));
        var layout = new StackPanel();
        layout.Children.Add(new AppText { Classes = { "policy-menu-title" }, Text = ApprovalPolicyText.Get("Title") });
        AddOption(layout, ApprovalPolicy.ReadOnly);
        AddOption(layout, ApprovalPolicy.AskEveryTime);
        AddOption(layout, ApprovalPolicy.FullAccess);
        _flyout = new AppFlyout
        {
            Placement = PlacementMode.TopEdgeAlignedLeft,
            DismissOnButtonClick = true,
            Content = new AppCard { Classes = { "flyout-surface", "card-inset-menu", "policy-menu" }, Child = layout }
        };
        Flyout = _flyout;
        UpdatePolicy();
    }

    public ApprovalPolicy Policy
    {
        get => GetValue(PolicyProperty);
        set => SetValue(PolicyProperty, value);
    }

    public ICommand? SelectPolicyCommand
    {
        get => GetValue(SelectPolicyCommandProperty);
        set => SetValue(SelectPolicyCommandProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == PolicyProperty && _label is not null) UpdatePolicy();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs args)
    {
        _flyout.Hide();
        base.OnDetachedFromVisualTree(args);
    }

    private void AddOption(StackPanel layout, ApprovalPolicy policy)
    {
        var text = ApprovalPolicyText.Label(policy);
        var row = new AppButton
        {
            Classes = { "ghost", "policy-option", "align-leading" },
            CommandParameter = policy,
            Content = new StackPanel
            {
                Children =
                {
                    new AppText { Classes = { "policy-option-title" }, Text = text },
                    new AppText { Classes = { "muted", "policy-option-hint" }, Text = ApprovalPolicyText.Get(policy + "Hint") }
                }
            }
        };
        AutomationProperties.SetName(row, text);
        row.Bind(CommandProperty, new Binding(nameof(SelectPolicyCommand)) { Source = this });
        row.Bind(IsEnabledProperty, new Binding(nameof(SelectPolicyCommand)) { Source = this, Converter = ObjectConverters.IsNotNull });
        _options.Add(policy, row);
        layout.Children.Add(row);
    }

    private void UpdatePolicy()
    {
        _label.Label = ApprovalPolicyText.Label(Policy);
        Classes.Set("policy-readonly", Policy == ApprovalPolicy.ReadOnly);
        Classes.Set("policy-ask", Policy == ApprovalPolicy.AskEveryTime);
        foreach (var option in _options) option.Value.Classes.Set("selected", option.Key == Policy);
        SetValue(AutomationProperties.NameProperty, _label.Label, BindingPriority.Style);
    }

    protected override Type StyleKeyOverride => typeof(AppButton);
}
