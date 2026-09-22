using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Application.Models;

public sealed record ModelRequest(
    AiProvider Provider,
    ModelProfile Model,
    IReadOnlyList<ModelMessage> Messages,
    IReadOnlyList<ToolDescriptor> Tools,
    string? PreviousResponseId = null);
