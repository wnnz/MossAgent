namespace MossAgent.Application.Models;

public sealed class PinnedModelContextException() : InvalidOperationException(
    "The fixed user context and latest turn cannot fit within the model context budget.");
