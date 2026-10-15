namespace MossAgent.Application.Models;

public sealed record ModelCompleted(string? ResponseId = null,
    [property: System.Text.Json.Serialization.JsonIgnore] ModelContinuation? Continuation = null) : ModelEvent;
