using MossAgent.Application.Models;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;

namespace MossAgent.Agent;

internal static class ModelContextWindowBuilder
{
    private const int CharactersPerToken = 4;
    private const int SafetyTokens = 512;
    private const string TruncationMarker = "\n…[上下文已裁剪]…\n";

    public static IReadOnlyList<ModelMessage> Build(
        IReadOnlyList<ModelMessage> messages,
        IReadOnlyList<ToolDescriptor> tools,
        ModelProfile model)
    {
        var available = CalculateAvailableCharacters(tools, model);
        var systemMessages = messages.Where(static message => message.Role == ModelRole.System).ToArray();
        var groups = GroupConversation(messages);
        var systemBudget = Math.Min(available / 4, Estimate(systemMessages));
        var selected = FitMessages(systemMessages, systemBudget);
        available -= Estimate(selected);

        var selectedGroups = new List<IReadOnlyList<ModelMessage>>();
        for (var index = groups.Count - 1; index >= 0; index--)
        {
            var group = groups[index];
            var cost = Estimate(group);
            if (cost <= available)
            {
                selectedGroups.Add(group);
                available -= cost;
                continue;
            }

            if (selectedGroups.Count == 0)
            {
                selectedGroups.Add(FitMessages(group, Math.Max(available, 256)));
            }

            break;
        }

        selectedGroups.Reverse();
        return selected.Concat(selectedGroups.SelectMany(static group => group)).ToArray();
    }

    internal static int Estimate(IReadOnlyList<ModelMessage> messages) =>
        messages.Sum(Estimate);

    private static int CalculateAvailableCharacters(
        IReadOnlyList<ToolDescriptor> tools,
        ModelProfile model)
    {
        var inputTokens = Math.Max(
            256,
            model.ContextLength - model.MaximumOutputTokens - SafetyTokens);
        var toolCharacters = tools.Sum(static tool =>
            tool.Name.Length + tool.Description.Length + tool.InputSchemaJson.Length + 64);
        return Math.Max(256, (inputTokens * CharactersPerToken) - toolCharacters);
    }

    private static List<IReadOnlyList<ModelMessage>> GroupConversation(
        IReadOnlyList<ModelMessage> messages)
    {
        var groups = new List<IReadOnlyList<ModelMessage>>();
        var current = new List<ModelMessage>();
        foreach (var message in messages.Where(static message => message.Role != ModelRole.System))
        {
            if (message.Role == ModelRole.User && current.Count > 0)
            {
                groups.Add(current.ToArray());
                current.Clear();
            }

            current.Add(message);
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }

    private static IReadOnlyList<ModelMessage> FitMessages(
        IReadOnlyList<ModelMessage> messages,
        int budget)
    {
        if (messages.Count == 0 || Estimate(messages) <= budget)
        {
            return messages;
        }

        var fixedCost = messages.Sum(EstimateMetadata);
        var contentBudget = Math.Max(0, budget - fixedCost);
        var share = Math.Max(0, contentBudget / messages.Count);
        return messages.Select(message => message with
        {
            Content = TrimContent(message.Content, share)
        }).ToArray();
    }

    private static int Estimate(ModelMessage message) =>
        message.Content.Length + EstimateMetadata(message);

    private static int EstimateMetadata(ModelMessage message) =>
        32 + (message.ToolCallId?.Length ?? 0) + (message.ToolName?.Length ?? 0)
        + (message.ToolCalls?.Sum(static call =>
            call.CallId.Length + call.Name.Length + call.Arguments.GetRawText().Length + 32) ?? 0);

    private static string TrimContent(string value, int maximumCharacters)
    {
        if (value.Length <= maximumCharacters)
        {
            return value;
        }

        if (maximumCharacters <= TruncationMarker.Length)
        {
            return string.Empty;
        }

        var retained = maximumCharacters - TruncationMarker.Length;
        var prefixLength = retained / 2;
        var suffixLength = retained - prefixLength;
        return value[..prefixLength] + TruncationMarker + value[^suffixLength..];
    }
}
