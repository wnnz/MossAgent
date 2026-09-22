namespace MossAgent.Application.Models;

public sealed record ModelFailed(string Code, string Message) : ModelEvent;
