namespace MossAgent.Application.Models;

/// <summary>
/// Joint model x protocol input validation executed before a request leaves the
/// process: reasoning effort, advanced parameters and image input must all be
/// within the selected model's declared capability for its protocol. Text input
/// (including PDF extracted-text document attachments) is always allowed;
/// violations produce explicit failures rather than silent substitution.
/// </summary>
public static class ModelCapabilityGate
{
    /// <summary>
    /// Returns a failure event describing the first capability violation, or
    /// null when the request fits the model's capability matrix.
    /// </summary>
    public static ModelFailed? Check(ModelRequest request)
    {
        if (!ModelReasoningPolicy.Supports(request.Provider.Protocol,
                request.Model.ModelId, request.Model.ReasoningEffort))
        {
            return new ModelFailed(ModelCapabilityException.UnsupportedReasoningEffort,
                "The selected model does not support the configured reasoning effort. "
                + "Choose a supported effort in model settings.");
        }

        if (ModelParameterPolicy.Check(request.Provider.Protocol,
                request.Model.AdvancedParametersJson, request.Model.MaximumOutputTokens) is { } violation)
        {
            return new ModelFailed(ModelCapabilityException.InvalidModelParameters,
                Describe(violation));
        }

        try
        {
            ModelImageRequestValidation.Validate(request);
        }
        catch (ModelCapabilityException capability)
        {
            return new ModelFailed(capability.Code, capability.Message);
        }

        return null;
    }

    /// <summary>
    /// Throws <see cref="ModelCapabilityException"/> when the request exceeds
    /// the model's capability matrix.
    /// </summary>
    public static void Validate(ModelRequest request)
    {
        if (Check(request) is { } failure)
        {
            throw new ModelCapabilityException(failure.Code, failure.Message);
        }
    }

    private static string Describe(ModelParameterViolation violation) => violation.Kind switch
    {
        ModelParameterViolationKind.NotJsonObject =>
            "The model's advanced parameters are not a valid JSON object. Fix them in model settings.",
        ModelParameterViolationKind.AlternateCompletionLimit =>
            $"Advanced parameter '{violation.Key}' must be a positive integer within the configured "
            + "output limit. Fix it in model settings.",
        ModelParameterViolationKind.MergeableNotObject =>
            $"Advanced parameter '{violation.Key}' must be a JSON object. Fix it in model settings.",
        ModelParameterViolationKind.ManagedNested =>
            $"Advanced parameter '{violation.Key}' overrides a field managed by the application. "
            + "Remove it in model settings.",
        _ =>
            $"Advanced parameter '{violation.Key}' overrides a field managed by the application. "
            + "Remove it in model settings."
    };
}
