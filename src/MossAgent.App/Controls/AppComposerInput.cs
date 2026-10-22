using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;

namespace MossAgent.App.Controls;

public sealed class AppComposerInput : AppInput
{
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<AppComposerInput, ICommand?>(nameof(SubmitCommand));
    public static readonly StyledProperty<ICommand?> AlternateSubmitCommandProperty =
        AvaloniaProperty.Register<AppComposerInput, ICommand?>(nameof(AlternateSubmitCommand));

    public static readonly StyledProperty<bool> IsSubmissionEnabledProperty =
        AvaloniaProperty.Register<AppComposerInput, bool>(nameof(IsSubmissionEnabled));
    public static readonly StyledProperty<bool> SubmitOnEnterProperty =
        AvaloniaProperty.Register<AppComposerInput, bool>(nameof(SubmitOnEnter), defaultValue: true);

    private TextPresenter? _presenter;
    private bool _enterHeld;

    public ICommand? SubmitCommand
    {
        get => GetValue(SubmitCommandProperty);
        set => SetValue(SubmitCommandProperty, value);
    }

    public bool IsSubmissionEnabled
    {
        get => GetValue(IsSubmissionEnabledProperty);
        set => SetValue(IsSubmissionEnabledProperty, value);
    }

    public bool SubmitOnEnter
    {
        get => GetValue(SubmitOnEnterProperty);
        set => SetValue(SubmitOnEnterProperty, value);
    }

    public ICommand? AlternateSubmitCommand
    {
        get => GetValue(AlternateSubmitCommandProperty);
        set => SetValue(AlternateSubmitCommandProperty, value);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs eventArgs)
    {
        base.OnApplyTemplate(eventArgs);
        _presenter = eventArgs.NameScope.Find<TextPresenter>("PART_TextPresenter");
        _enterHeld = false;
    }

    protected override void OnKeyDown(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key != Key.Enter)
        {
            base.OnKeyDown(eventArgs);
            return;
        }

        var wasHeld = _enterHeld;
        _enterHeld = true;
        if (eventArgs.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Control))
        {
            base.OnKeyDown(eventArgs);
            return;
        }
        if (eventArgs.KeyModifiers == KeyModifiers.None && !SubmitOnEnter)
        {
            base.OnKeyDown(eventArgs);
            return;
        }

        eventArgs.Handled = true;
        var command = eventArgs.KeyModifiers == KeyModifiers.Control ? AlternateSubmitCommand ?? SubmitCommand : SubmitCommand;
        if (!wasHeld && IsFocused && IsEffectivelyVisible && IsEffectivelyEnabled && !IsReadOnly
            && VisualRoot is not null && IsSubmissionEnabled && !string.IsNullOrWhiteSpace(Text)
            && string.IsNullOrEmpty(_presenter?.PreeditText) && command is not null
            && command.CanExecute(null))
            command.Execute(null);
    }

    protected override void OnKeyUp(KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.Enter) _enterHeld = false;
        base.OnKeyUp(eventArgs);
    }

    protected override void OnLostFocus(FocusChangedEventArgs eventArgs)
    {
        _enterHeld = false;
        base.OnLostFocus(eventArgs);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArgs)
    {
        _enterHeld = false;
        base.OnDetachedFromVisualTree(eventArgs);
    }
}
