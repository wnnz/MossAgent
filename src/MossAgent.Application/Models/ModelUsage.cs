namespace MossAgent.Application.Models;

public sealed record ModelUsage(
    int InputTokens,
    int OutputTokens) : ModelEvent;

