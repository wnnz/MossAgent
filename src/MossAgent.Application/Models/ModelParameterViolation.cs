namespace MossAgent.Application.Models;

/// <summary>
/// The first advanced-parameter violation found by
/// <see cref="ModelParameterPolicy.Check"/>. <see cref="Key"/> names the
/// offending JSON property so callers can produce an actionable explanation.
/// </summary>
public sealed record ModelParameterViolation(ModelParameterViolationKind Kind, string? Key);
