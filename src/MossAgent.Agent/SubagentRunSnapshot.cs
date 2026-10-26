using System.Buffers;
using System.Text;
using System.Text.Json;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

/// <summary>
/// Builds the bounded resolved-configuration snapshot persisted with every journaled
/// subagent run: the selected profile, provider, model, reasoning effort, context sharing
/// and a task preview. Previews are compressed rather than rejected so the journal never
/// blocks a legitimate run.
/// </summary>
internal static class SubagentRunSnapshot
{
    internal const int MaximumSnapshotLength = 16_384;
    private const int MaximumProfileNameLength = 256;
    private static readonly (int Prompt, int Task)[] PreviewBudgets = [(1_024, 2_048), (256, 512), (32, 64)];

    public static string ProfileLabel(SubagentProfile profile) => Bounded(profile.Name, MaximumProfileNameLength);

    public static string Create(SubagentProfile profile, AiProvider provider, ModelProfile model,
        string task, ToolExecutionContext context)
    {
        var sharedImages = profile.ShareContext ? context.SubagentImages?.Count ?? 0 : 0;
        foreach (var (promptLimit, taskLimit) in PreviewBudgets)
        {
            var json = Write(profile, provider, model, task, promptLimit, taskLimit, sharedImages);
            if (json.Length <= MaximumSnapshotLength) return json;
        }
        return Write(profile, provider, model, task, 8, 16, sharedImages);
    }

    private static string Write(SubagentProfile profile, AiProvider provider, ModelProfile model,
        string task, int promptLimit, int taskLimit, int sharedImages)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("profile", ProfileLabel(profile));
            writer.WriteString("systemPromptPreview", Bounded(profile.SystemPrompt, promptLimit));
            writer.WriteNumber("systemPromptLength", profile.SystemPrompt.Length);
            writer.WriteString("providerId", provider.Id.ToString("D"));
            writer.WriteString("providerProtocol", provider.Protocol.ToString());
            writer.WriteString("modelProfileId", model.Id.ToString("D"));
            writer.WriteString("modelId", Bounded(model.ModelId, 512));
            writer.WriteString("reasoningEffort", Bounded(profile.ReasoningEffort, 128));
            writer.WriteBoolean("shareContext", profile.ShareContext);
            writer.WriteString("taskPreview", Bounded(task, taskLimit));
            writer.WriteNumber("taskLength", task.Length);
            writer.WriteNumber("sharedImageCount", sharedImages);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static string Bounded(string value, int limit)
    {
        if (value.Length <= limit) return value;
        var length = limit;
        if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
        return value[..length];
    }
}
