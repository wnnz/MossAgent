namespace MossAgent.Application.Plugins;

/// <summary>Materializes the skill components of enabled plugins for one task dispatch: reads
/// each skill file under the plugin install root, joins the survivors into a bounded instruction
/// block, and persists exactly the included skills as that task's activation snapshot before
/// returning the text.</summary>
public interface IPluginSkillActivator
{
    /// <summary>Returns the joined skill instructions, or null when no enabled skill was
    /// included. The task's snapshot rows are rewritten to match the returned set even when
    /// the result is null; storage failures propagate.</summary>
    Task<string?> BuildSkillInstructionsAsync(Guid taskId, CancellationToken cancellationToken);
}
