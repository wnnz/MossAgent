namespace MossAgent.Application.Models;

/// <summary>
/// A request input violates the selected model's capability matrix. Carries a
/// stable <see cref="Code"/> so callers can map the failure to a localized,
/// actionable explanation instead of silently substituting the input.
/// </summary>
public sealed class ModelCapabilityException(string code, string message)
    : ArgumentException(message)
{
    public const string UnsupportedImageInput = "model_image_input_unsupported";
    public const string UnsupportedReasoningEffort = "unsupported_reasoning_effort";
    public const string InvalidModelParameters = "invalid_model_parameters";

    public string Code { get; } = code;
}
