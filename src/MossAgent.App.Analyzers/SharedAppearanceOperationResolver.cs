using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MossAgent.App.Analyzers;

internal static class SharedAppearanceOperationResolver
{
    public static IOperation? Resolve(IOperation? operation, SemanticModel? model, CancellationToken cancellationToken)
    {
        for (var depth = 0; operation is not null && depth < 32; depth++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (operation is IConversionOperation conversion) operation = conversion.Operand;
            else if (operation is IUnaryOperation unary) operation = unary.Operand;
            else if (operation is IParenthesizedOperation parenthesized) operation = parenthesized.Operand;
            else if (operation is ILocalReferenceOperation local && model is not null
                && StableInitializer(local.Local, model, cancellationToken) is { } initializer)
                operation = model.GetOperation(initializer, cancellationToken);
            else if (operation is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver }
                && InitializedMember(operation) is { } member)
                operation = member;
            else break;
        }
        return operation;
    }

    private static IOperation? InitializedMember(IOperation operation)
    {
        var child = operation;
        for (var depth = 0; child.Parent is { } parent && depth < 32; depth++)
        {
            if (parent is IMemberInitializerOperation member)
                return ReferenceEquals(child, member.InitializedMember) ? null : member.InitializedMember;
            if (parent is IObjectCreationOperation) break;
            child = parent;
        }
        return null;
    }

    private static ExpressionSyntax? StableInitializer(ILocalSymbol local, SemanticModel model,
        CancellationToken cancellationToken)
    {
        var declaration = local.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax(cancellationToken)
            as VariableDeclaratorSyntax;
        if (declaration?.Initializer is null || declaration.SyntaxTree != model.SyntaxTree) return null;
        var scope = declaration.FirstAncestorOrSelf<BlockSyntax>();
        if (scope is null) return null;

        foreach (var identifier in scope.DescendantNodes().OfType<IdentifierNameSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (identifier.Identifier.ValueText != local.Name || !MayWriteLocal(identifier)) continue;
            if (SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(identifier, cancellationToken).Symbol, local))
                return null;
        }
        return UnwrapInitializer(declaration.Initializer.Value);
    }

    private static ExpressionSyntax UnwrapInitializer(ExpressionSyntax expression)
    {
        while (true)
        {
            if (expression is ParenthesizedExpressionSyntax parenthesized)
                expression = parenthesized.Expression;
            else if (expression is PostfixUnaryExpressionSyntax postfix
                && postfix.IsKind(SyntaxKind.SuppressNullableWarningExpression))
                expression = postfix.Operand;
            else return expression;
        }
    }

    private static bool MayWriteLocal(IdentifierNameSyntax identifier)
    {
        if (identifier.Parent is ArgumentSyntax argument && argument.RefKindKeyword.Kind()
            is SyntaxKind.RefKeyword or SyntaxKind.OutKeyword) return true;
        if (identifier.Parent is RefExpressionSyntax) return true;
        if (identifier.Parent?.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PreDecrementExpression
            or SyntaxKind.PostIncrementExpression or SyntaxKind.PostDecrementExpression) return true;

        return identifier.Ancestors().OfType<AssignmentExpressionSyntax>().Any(assignment =>
            assignment.Left is IdentifierNameSyntax or TupleExpressionSyntax or ParenthesizedExpressionSyntax
            && assignment.Left.Span.Contains(identifier.Span));
    }
}
