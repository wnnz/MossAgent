using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MossAgent.App.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedControlAppearanceAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MOSSUI002";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use shared component appearance",
        "Use a shared component variant in Styles/ instead of overriding '{0}'",
        "MossAgent.UI",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment,
            OperationKind.CompoundAssignment, OperationKind.DeconstructionAssignment,
            OperationKind.Increment, OperationKind.Decrement);
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeCreation, OperationKind.ObjectCreation);
    }

    private static bool IsWrapperCode(OperationAnalysisContext context) =>
        context.ContainingSymbol?.ContainingNamespace.ToDisplayString() == "MossAgent.App.Controls";

    private static void AnalyzeAssignment(OperationAnalysisContext context)
    {
        if (IsWrapperCode(context)) return;
        var target = context.Operation switch
        {
            IAssignmentOperation assignment => assignment.Target,
            IIncrementOrDecrementOperation increment => increment.Target,
            _ => null
        };
        AnalyzeAssignmentTarget(context, target);
    }

    private static void AnalyzeAssignmentTarget(OperationAnalysisContext context, IOperation? target)
    {
        if (target is ITupleOperation tuple)
        {
            foreach (var element in tuple.Elements) AnalyzeAssignmentTarget(context, element);
            return;
        }
        if (target is not IPropertyReferenceOperation property) return;
        if (SharedStyleClassPolicy.IsForbiddenAssignment(property,
            context.Operation.SemanticModel, context.CancellationToken))
        {
            ReportAppearanceCollection(context, "Classes");
            return;
        }
        if (property.Property.IsIndexer
            && AppearanceCollectionProperty(context, property.Instance) is { } collectionProperty)
        {
            ReportAppearanceCollection(context, collectionProperty);
            return;
        }
        var indexArgument = property.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0);
        var name = property.Property.IsIndexer
            ? indexArgument is null ? null : StyledPropertyName(context, indexArgument.Value)
            : property.Property.Name;
        if (name is not null) ReportIfForbidden(context, property.Instance, name);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        if (IsWrapperCode(context)) return;
        var invocation = (IInvocationOperation)context.Operation;
        var offset = invocation.TargetMethod.IsExtensionMethod && invocation.Instance is null ? 1 : 0;
        var target = invocation.Instance ?? (offset == 1
            ? invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == 0)?.Value : null);
        if (SharedStyleClassPolicy.IsForbiddenMutation(invocation, target,
            context.Operation.SemanticModel, context.CancellationToken))
        {
            ReportAppearanceCollection(context, "Classes");
            return;
        }
        if (invocation.TargetMethod.Name is "Add" or "TryAdd" or "AddRange" or "Insert" or "InsertRange"
            or "Remove" or "RemoveAt" or "RemoveAll" or "RemoveRange" or "Clear" or "Move" or "Set"
            && AppearanceCollectionProperty(context, target) is { } collectionProperty)
        {
            ReportAppearanceCollection(context, collectionProperty);
            return;
        }
        if (invocation.TargetMethod.Name is not ("SetValue" or "SetCurrentValue" or "Bind" or "ClearValue")
            || invocation.Arguments.Length == 0) return;

        var property = invocation.Arguments.FirstOrDefault(argument => argument.Parameter?.Ordinal == offset);
        if (property is null) return;
        var name = StyledPropertyName(context, property.Value);
        if (name is not null) ReportIfForbidden(context, target, name);
    }

    private static string? AppearanceCollectionProperty(OperationAnalysisContext context, IOperation? operation)
    {
        for (var depth = 0; operation is not null && depth < 32; depth++)
        {
            operation = SharedAppearanceOperationResolver.Resolve(operation,
                context.Operation.SemanticModel, context.CancellationToken);
            if (operation is not IPropertyReferenceOperation property) break;
            if (property.Property.Name is "Styles" or "FlyoutPresenterClasses" or "StrokeDashArray"
                    or "Children" or "ColumnDefinitions" or "RowDefinitions" or "Resources" or "DataTemplates"
                && IsForbiddenTarget(context, property.Instance, property.Property.Name))
                return property.Property.Name;
            if (!property.Property.IsIndexer
                && property.Property.Name is not ("MergedDictionaries" or "ThemeDictionaries")) break;
            operation = property.Instance;
        }
        return null;
    }

    private static void ReportAppearanceCollection(OperationAnalysisContext context, string property) =>
        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), property));

    private static string? StyledPropertyName(OperationAnalysisContext context, IOperation operation)
    {
        var resolved = SharedAppearanceOperationResolver.Resolve(operation,
            context.Operation.SemanticModel, context.CancellationToken);
        return resolved is IFieldReferenceOperation field
            && field.Field.Name.EndsWith("Property", StringComparison.Ordinal)
                ? field.Field.Name.Substring(0, field.Field.Name.Length - "Property".Length) : null;
    }

    private static void AnalyzeCreation(OperationAnalysisContext context)
    {
        if (IsWrapperCode(context)) return;
        var creation = (IObjectCreationOperation)context.Operation;
        if (creation.Type?.ToDisplayString() is not ("Avalonia.Styling.Style" or "Avalonia.Styling.ControlTheme"))
            return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, creation.Syntax.GetLocation(), creation.Type.Name));
    }

    private static void ReportIfForbidden(OperationAnalysisContext context, IOperation? target, string property)
    {
        if (IsForbiddenTarget(context, target, property))
            context.ReportDiagnostic(Diagnostic.Create(Rule, context.Operation.Syntax.GetLocation(), property));
    }

    private static bool IsForbiddenTarget(OperationAnalysisContext context, IOperation? target, string property)
    {
        while (target is not null)
        {
            if (SharedAppearancePropertyPolicy.IsForbidden(target.Type, property)) return true;
            var resolved = SharedAppearanceOperationResolver.Resolve(target,
                context.Operation.SemanticModel, context.CancellationToken);
            target = ReferenceEquals(resolved, target) ? null : resolved;
        }
        return false;
    }
}
