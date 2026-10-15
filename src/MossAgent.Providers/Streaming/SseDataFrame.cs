using MossAgent.Application.Models;

namespace MossAgent.Providers.Streaming;

internal readonly record struct SseDataFrame(string? Data, ModelFailed? Failure);
