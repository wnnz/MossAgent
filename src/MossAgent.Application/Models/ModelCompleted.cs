namespace MossAgent.Application.Models;

public sealed record ModelCompleted(string? ResponseId = null) : ModelEvent;

