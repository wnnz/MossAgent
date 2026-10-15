namespace MossAgent.App.Tests;

internal enum GeminiStreamScenario
{
    SelectedCandidate,
    PrematureEof,
    BlockedFinish,
    BlockedPrompt,
    OutputLimit,
    MalformedAfterStop,
    LateText,
    LateTool,
    InvalidSecondToolArguments,
    OversizedLineAfterStop,
    OversizedEventAfterStop,
    SignedSteps,
    InvalidSignature,
    EmptySignature,
    OversizedSignature,
    InvalidNativeCallId,
    DuplicateNativeCallId,
    MissingRequiredSignature,
    SignedParallelSteps
}
