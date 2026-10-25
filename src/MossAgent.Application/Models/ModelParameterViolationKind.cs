namespace MossAgent.Application.Models;

public enum ModelParameterViolationKind
{
    NotJsonObject,
    ManagedRoot,
    MergeableNotObject,
    ManagedNested,
    AlternateCompletionLimit
}
