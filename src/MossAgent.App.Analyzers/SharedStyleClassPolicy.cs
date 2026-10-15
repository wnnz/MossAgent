using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace MossAgent.App.Analyzers;

internal static class SharedStyleClassPolicy
{
    public static bool IsForbiddenAssignment(IPropertyReferenceOperation property,
        SemanticModel? model, CancellationToken cancellationToken) =>
        IsClasses(property, model, cancellationToken)
        || property.Property.IsIndexer && IsClasses(property.Instance, model, cancellationToken);

    public static bool IsForbiddenMutation(IInvocationOperation invocation, IOperation? target,
        SemanticModel? model, CancellationToken cancellationToken)
    {
        if (!IsClasses(target, model, cancellationToken)) return false;
        if (invocation.TargetMethod.Name is "Clear" or "RemoveAt" or "RemoveAll" or "RemoveRange"
            or "AddRange" or "InsertRange" or "Move" or "MoveRange" or "Reset") return true;
        if (invocation.TargetMethod.Name is not ("Add" or "Remove" or "Insert" or "Set")) return false;

        var argument = invocation.Arguments.FirstOrDefault(value =>
            value.Parameter?.Type.SpecialType == SpecialType.System_String);
        var resolved = SharedAppearanceOperationResolver.Resolve(argument?.Value, model, cancellationToken);
        return resolved?.ConstantValue.Value is not string name
            || name.StartsWith("app-", StringComparison.Ordinal);
    }

    private static bool IsClasses(IOperation? operation, SemanticModel? model,
        CancellationToken cancellationToken) =>
        SharedAppearanceOperationResolver.Resolve(operation, model, cancellationToken)
            is IPropertyReferenceOperation { Property.Name: "Classes" } property
        && property.Property.ContainingType.ToDisplayString() == "Avalonia.StyledElement";
}
