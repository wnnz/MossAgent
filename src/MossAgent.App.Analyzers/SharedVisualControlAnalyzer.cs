using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace MossAgent.App.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class SharedVisualControlAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "MOSSUI001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Use a shared visual component",
        "Use shared MossAgent.App.Controls components for '{0}'; reusable visual implementations belong in Controls and page hosts in Views",
        "MossAgent.UI",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        customTags: [WellKnownDiagnosticTags.NotConfigurable]);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeCreation,
            SyntaxKind.ObjectCreationExpression, SyntaxKind.ImplicitObjectCreationExpression);
        context.RegisterSyntaxNodeAction(AnalyzeBaseType,
            SyntaxKind.SimpleBaseType, SyntaxKind.PrimaryConstructorBaseType);
        context.RegisterOperationAction(AnalyzeActivation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
    }

    private static void AnalyzeCreation(SyntaxNodeAnalysisContext context)
    {
        if (IsWrapperCode(context)) return;
        if (context.SemanticModel.GetTypeInfo(context.Node, context.CancellationToken).Type
            is not ITypeSymbol type) return;
        var forbidden = IsForbiddenVisual(type, context.Compilation) ? type
            : ForbiddenConstructionArgument(type as INamedTypeSymbol, context.Compilation);
        if (forbidden is null) return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, context.Node.GetLocation(), forbidden.Name));
    }

    private static void AnalyzeBaseType(SyntaxNodeAnalysisContext context)
    {
        if (IsWrapperCode(context)) return;
        var baseType = (BaseTypeSyntax)context.Node;
        if (context.SemanticModel.GetTypeInfo(baseType.Type, context.CancellationToken).Type
            is not INamedTypeSymbol type || !IsVisual(type, context.Compilation)
            || IsStructuralLayout(type)) return;
        if (context.ContainingSymbol?.ContainingNamespace.ToDisplayString() == "MossAgent.App.Views"
            && type.ToDisplayString() is "MossAgent.App.Controls.AppUserControl"
                or "MossAgent.App.Controls.AppWindow") return;

        context.ReportDiagnostic(Diagnostic.Create(Rule, baseType.Type.GetLocation(), type.Name));
    }

    private static void AnalyzeActivation(OperationAnalysisContext context)
    {
        if (context.ContainingSymbol?.ContainingNamespace.ToDisplayString() == "MossAgent.App.Controls") return;
        var invocation = (IInvocationOperation)context.Operation;
        if (ForbiddenConstructionArgument(invocation.TargetMethod, context.Compilation) is { } forbidden)
        {
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), forbidden.Name));
            return;
        }
        if (invocation.TargetMethod.ContainingType.ToDisplayString() != "System.Activator"
            || invocation.TargetMethod.Name != "CreateInstance") return;

        var type = invocation.TargetMethod.TypeArguments.FirstOrDefault();
        if (type is null)
        {
            var argument = invocation.Arguments.FirstOrDefault(value => value.Parameter?.Name == "type");
            var resolved = SharedAppearanceOperationResolver.Resolve(argument?.Value,
                context.Operation.SemanticModel, context.CancellationToken);
            type = (resolved as ITypeOfOperation)?.TypeOperand;
        }
        if (type is not null && IsForbiddenVisual(type, context.Compilation))
            context.ReportDiagnostic(Diagnostic.Create(Rule, invocation.Syntax.GetLocation(), type.Name));
    }

    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        if (context.ContainingSymbol?.ContainingNamespace.ToDisplayString() == "MossAgent.App.Controls") return;
        var reference = (IMethodReferenceOperation)context.Operation;
        if (ForbiddenConstructionArgument(reference.Method, context.Compilation) is { } forbidden)
            context.ReportDiagnostic(Diagnostic.Create(Rule, reference.Syntax.GetLocation(), forbidden.Name));
    }

    private static ITypeSymbol? ForbiddenConstructionArgument(IMethodSymbol method, Compilation compilation) =>
        ForbiddenConstructionArgument(method.TypeParameters, method.TypeArguments, compilation)
        ?? ForbiddenConstructionArgument(method.ContainingType, compilation);

    private static ITypeSymbol? ForbiddenConstructionArgument(INamedTypeSymbol? type, Compilation compilation)
    {
        for (var current = type; current is not null; current = current.ContainingType)
        {
            var forbidden = ForbiddenConstructionArgument(current.TypeParameters, current.TypeArguments, compilation);
            if (forbidden is not null) return forbidden;
        }
        return null;
    }

    private static ITypeSymbol? ForbiddenConstructionArgument(ImmutableArray<ITypeParameterSymbol> parameters,
        ImmutableArray<ITypeSymbol> arguments, Compilation compilation)
    {
        for (var index = 0; index < parameters.Length; index++)
        {
            if (parameters[index].HasConstructorConstraint && IsForbiddenVisual(arguments[index], compilation))
                return arguments[index];
        }
        return null;
    }

    private static bool IsWrapperCode(SyntaxNodeAnalysisContext context) =>
        context.ContainingSymbol?.ContainingNamespace.ToDisplayString() == "MossAgent.App.Controls";

    private static bool IsForbiddenVisual(ITypeSymbol type, Compilation compilation)
    {
        if (type is ITypeParameterSymbol parameter)
            return parameter.ConstraintTypes.Any(constraint => IsForbiddenVisual(constraint, compilation));
        if (!IsVisual(type, compilation)) return false;
        if (type.ContainingNamespace.ToDisplayString() == "MossAgent.App.Controls") return false;
        if (type is INamedTypeSymbol namedType && IsViewHost(namedType)) return false;

        return !IsStructuralLayout(type);
    }

    private static bool IsStructuralLayout(ITypeSymbol type) =>
        type.ToDisplayString() is "Avalonia.Controls.Grid" or "Avalonia.Controls.StackPanel"
            or "Avalonia.Controls.WrapPanel" or "Avalonia.Controls.Panel"
            or "Avalonia.Controls.Presenters.ContentPresenter";

    private static bool IsVisual(ITypeSymbol type, Compilation compilation)
    {
        var control = compilation.GetTypeByMetadataName("Avalonia.Controls.Control");
        var flyout = compilation.GetTypeByMetadataName("Avalonia.Controls.Primitives.FlyoutBase");
        return Inherits(type, control) || Inherits(type, flyout);
    }

    private static bool IsViewHost(INamedTypeSymbol type)
    {
        if (type.ContainingNamespace.ToDisplayString() != "MossAgent.App.Views") return false;
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() is "MossAgent.App.Controls.AppUserControl"
                or "MossAgent.App.Controls.AppWindow") return true;
        }
        return false;
    }

    private static bool Inherits(ITypeSymbol type, INamedTypeSymbol? ancestor)
    {
        if (ancestor is null) return false;
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, ancestor)) return true;
        }
        return false;
    }
}
