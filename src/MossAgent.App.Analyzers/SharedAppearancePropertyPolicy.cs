using Microsoft.CodeAnalysis;

namespace MossAgent.App.Analyzers;

internal static class SharedAppearancePropertyPolicy
{
    public static bool IsForbidden(ITypeSymbol? candidate, string property)
    {
        if (candidate is ITypeParameterSymbol parameter)
            return parameter.ConstraintTypes.Any(constraint => IsForbidden(constraint, property));
        if (candidate is not INamedTypeSymbol type) return false;
        if (Inherits(type, "Avalonia.Controls.Primitives.FlyoutBase"))
            return property is "FlyoutPresenterTheme" or "FlyoutPresenterClasses";
        if (!Inherits(type, "Avalonia.Controls.Control")) return false;
        if (Inherits(type, "MossAgent.App.Controls.AppFileBreadcrumb")
            && property is "Children" or "Width" or "Height" or "MinWidth" or "MaxWidth" or "MinHeight" or "MaxHeight") return true;
        if (Inherits(type, "MossAgent.App.Controls.AppConversationTab") && property is "ContextMenu" or "Flyout") return true;
        if ((Inherits(type, "MossAgent.App.Controls.AppCodePreview")
                || Inherits(type, "MossAgent.App.Controls.AppFilePreview")
                || Inherits(type, "MossAgent.App.Controls.AppImagePreview")
                || Inherits(type, "MossAgent.App.Controls.AppResponseContent")
                || Inherits(type, "MossAgent.App.Controls.AppFileChangeSummary")
                || Inherits(type, "MossAgent.App.Controls.AppModelPickerMenu")
                || Inherits(type, "MossAgent.App.Controls.AppTerminalPane")
                || Inherits(type, "MossAgent.App.Controls.AppTerminalViewport")
                || Inherits(type, "MossAgent.App.Controls.AppTerminalScreen")
                || Inherits(type, "MossAgent.App.Controls.AppResponseTable"))
            && property is "Content" or "DataTemplates" or "Padding" or "Width" or "Height"
                or "MinWidth" or "MaxWidth" or "MinHeight" or "MaxHeight"
                or "HorizontalContentAlignment" or "VerticalContentAlignment") return true;
        if (OwnsActions(type, property) || OwnsWorkspaceGeometry(type, property)) return true;
        if (Inherits(type, "MossAgent.App.Controls.AppTerminalScreen") && property == "CellHeight") return true;
        if (property is "Background" or "Foreground" or "BorderBrush" or "BorderThickness"
            or "CornerRadius" or "BoxShadow" or "FontSize" or "FontWeight" or "FontFamily"
            or "CodeFontFamily" or "CodeFontSize" or "CodeBackground" or "CodeBackgroundPadding" or "CodeCornerRadius"
            or "SelectionBrush" or "SelectionForegroundBrush"
            or "LineHeight" or "Stroke" or "Fill" or "StrokeThickness" or "StrokeLineCap"
            or "StrokeJoin" or "StrokeMiterLimit" or "StrokeDashArray" or "StrokeDashOffset"
            or "RenderTransform" or "RenderTransformOrigin" or "Cursor"
            or "Template" or "Theme" or "FocusAdorner" or "Styles" or "Resources") return true;

        if (property == "Padding" && (OwnsComposedSurface(type)
            || Inherits(type, "Avalonia.Controls.Button")
            || Inherits(type, "Avalonia.Controls.TextBox")
            || Inherits(type, "Avalonia.Controls.ComboBox")
            || Inherits(type, "Avalonia.Controls.NumericUpDown")
            || Inherits(type, "Avalonia.Controls.ListBox")
            || Inherits(type, "MossAgent.App.Controls.AppCard")
            || Inherits(type, "MossAgent.App.Controls.AppSettingsDialog"))) return true;

        if (property == "Size" && Inherits(type, "MossAgent.App.Controls.AppIcon")) return true;
        if (property == "LabelMaxWidth")
            return Inherits(type, "MossAgent.App.Controls.AppIconLabel")
                || Inherits(type, "MossAgent.App.Controls.AppActionButton");
        if (property is "Height" or "MinHeight" or "MaxHeight"
            or "HorizontalContentAlignment" or "VerticalContentAlignment"
            && (Inherits(type, "Avalonia.Controls.TextBox")
                || Inherits(type, "Avalonia.Controls.ComboBox")
                || Inherits(type, "Avalonia.Controls.NumericUpDown"))) return true;
        if (property is "Width" or "Height" or "MinWidth" or "MaxWidth" or "MinHeight" or "MaxHeight"
            or "HorizontalContentAlignment" or "VerticalContentAlignment")
            return OwnsControlGeometry(type);

        if (property is "Spacing" or "Orientation" or "ColumnSpacing" or "RowSpacing"
            or "Children" or "ColumnDefinitions" or "RowDefinitions")
            return Inherits(type, "MossAgent.App.Controls.AppIconLabel");
        if (property == "Content") return OwnsContent(type);

        return false;
    }

    private static bool OwnsContent(INamedTypeSymbol type) =>
        OwnsComposedSurface(type)
        || Inherits(type, "MossAgent.App.Controls.AppPageSwitcher")
        || Inherits(type, "MossAgent.App.Controls.AppIconButton")
        || Inherits(type, "MossAgent.App.Controls.AppActionButton")
        || Inherits(type, "MossAgent.App.Controls.AppToolActivityRow")
        || Inherits(type, "MossAgent.App.Controls.AppBrowserToolbar")
        || Inherits(type, "MossAgent.App.Controls.AppSettingsNavButton");

    private static bool OwnsControlGeometry(INamedTypeSymbol type) =>
        OwnsComposedSurface(type)
        || Inherits(type, "Avalonia.Controls.Button")
        || Inherits(type, "MossAgent.App.Controls.AppIcon")
        || Inherits(type, "MossAgent.App.Controls.AppIconLabel")
        || Inherits(type, "MossAgent.App.Controls.AppColorSwatch")
        || Inherits(type, "MossAgent.App.Controls.AppViewbox")
        || Inherits(type, "MossAgent.App.Controls.AppToolActivityRow")
        || Inherits(type, "MossAgent.App.Controls.AppSettingsDialog");

    private static bool OwnsComposedSurface(INamedTypeSymbol type) =>
        Inherits(type, "MossAgent.App.Controls.AppAttachmentChip")
        || Inherits(type, "MossAgent.App.Controls.AppTaskFailureNotice")
        || Inherits(type, "MossAgent.App.Controls.AppFileChangeSummary")
        || Inherits(type, "MossAgent.App.Controls.AppProviderSettingsHeader")
        || Inherits(type, "MossAgent.App.Controls.AppModelSettingsRow")
        || Inherits(type, "MossAgent.App.Controls.AppSettingsItemRow")
        || Inherits(type, "MossAgent.App.Controls.AppSettingsPageHeader")
        || Inherits(type, "MossAgent.App.Controls.AppTaskNavigationRow")
        || Inherits(type, "MossAgent.App.Controls.AppConversationTab")
        || Inherits(type, "MossAgent.App.Controls.AppProjectNavigationRow")
        || Inherits(type, "MossAgent.App.Controls.AppSidebarSearch")
        || Inherits(type, "MossAgent.App.Controls.AppNavigationRail")
        || Inherits(type, "MossAgent.App.Controls.AppInspectorHeader")
        || Inherits(type, "MossAgent.App.Controls.AppEmptyState")
        || Inherits(type, "MossAgent.App.Controls.AppAuditExecutionRow")
        || Inherits(type, "MossAgent.App.Controls.AppProjectPrompt")
        || Inherits(type, "MossAgent.App.Controls.AppComposerContextBar")
        || Inherits(type, "MossAgent.App.Controls.AppConversationHeader");

    private static bool OwnsActions(INamedTypeSymbol type, string property) =>
        property is "Content" or "Flyout" or "Command" or "CommandParameter"
        && (Inherits(type, "MossAgent.App.Controls.AppEditMenu")
            || Inherits(type, "MossAgent.App.Controls.AppApprovalPolicyButton"));

    private static bool OwnsWorkspaceGeometry(INamedTypeSymbol type, string property)
    {
        if (Inherits(type, "MossAgent.App.Controls.AppWorkspacePanes"))
            return property is "Content" or "Padding" or "Width" or "Height" or "MinWidth" or "MaxWidth"
                or "MinHeight" or "MaxHeight" or "HorizontalContentAlignment" or "VerticalContentAlignment"
                or "MinimumPrimaryWidth" or "MinimumInspectorWidth" or "DefaultInspectorFraction"
                or "FloatingInspectorWidth" or "FloatingInspectorMargin";
        return Inherits(type, "MossAgent.App.Controls.AppPaneSplitter")
            && property is "Width" or "Height" or "MinWidth" or "MaxWidth" or "MinHeight" or "MaxHeight"
                or "ResizeDirection" or "ResizeBehavior" or "DragIncrement" or "KeyboardIncrement" or "ShowsPreview";
    }

    private static bool Inherits(INamedTypeSymbol type, string ancestor)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == ancestor) return true;
        }
        return false;
    }
}
