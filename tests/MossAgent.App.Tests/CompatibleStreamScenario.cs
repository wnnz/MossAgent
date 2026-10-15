namespace MossAgent.App.Tests;

internal enum CompatibleStreamScenario
{
    PrematureEof,
    MalformedAfterToolFinish,
    InvalidSecondToolArguments,
    MultipleChoices,
    OversizedLineAfterToolFinish,
    OversizedEventAfterToolFinish
}
