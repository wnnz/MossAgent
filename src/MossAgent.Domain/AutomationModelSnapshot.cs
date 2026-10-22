namespace MossAgent.Domain;

/// <summary>
/// Model parameters saved with a plan. A scheduled run reproduces these values instead of the
/// provider's current settings, so editing a model later cannot silently change an existing plan.
/// </summary>
/// <param name="ReasoningEffort">Thinking effort sent with the request; empty means the protocol default.</param>
/// <param name="Temperature">Sampling temperature, or null when not configured.</param>
/// <param name="TopP">Nucleus sampling value, or null when not configured.</param>
/// <param name="AdvancedParametersJson">Protocol-specific extra parameters as the user entered them.</param>
public sealed record AutomationModelSnapshot(
    string ReasoningEffort,
    double? Temperature,
    double? TopP,
    string AdvancedParametersJson);
