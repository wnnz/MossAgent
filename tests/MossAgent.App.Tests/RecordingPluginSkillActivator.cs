using MossAgent.Application.Plugins;

namespace MossAgent.App.Tests;

/// <summary>Returns a fixed instruction block and records which tasks asked for activation, so
/// executor tests can prove both the wiring and the review-chat skip.</summary>
internal sealed class RecordingPluginSkillActivator(string? text) : IPluginSkillActivator
{
    public List<Guid> TaskIds { get; } = [];

    public Task<string?> BuildSkillInstructionsAsync(Guid taskId, CancellationToken cancellationToken)
    {
        TaskIds.Add(taskId);
        return Task.FromResult(text);
    }
}
