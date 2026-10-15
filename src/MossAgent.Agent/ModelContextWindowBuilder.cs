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
        var systemBudget = Math.Min(available / 4, Estimate(systemMessages));
        var selected = FitMessages(systemMessages, systemBudget);
        available -= Estimate(selected);

        var selectedGroups = new List<IReadOnlyList<ModelMessage>>();
        var end = messages.Count;
        while (end > 0)
        {
            var group = ReadPreviousTurn(messages, ref end);
            if (group.Count == 0) break;
            var cost = Estimate(group);
            if (cost <= available)
            {
                selectedGroups.Add(group);
                available -= cost;
                continue;
            }

            if (available >= 256 && group.Sum(EstimateMetadata) <= available)
            {
                selectedGroups.Add(FitMessages(group, available));
            }
            else if (selectedGroups.Count == 0)
                throw new InvalidOperationException("The latest conversation metadata exceeds the model context budget.");

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

    private static IReadOnlyList<ModelMessage> ReadPreviousTurn(
        IReadOnlyList<ModelMessage> messages,
        ref int end)
    {
        var start = end;
        var count = 0;
        while (start > 0)
        {
            var message = messages[--start];
            if (message.Role == ModelRole.System) continue;
            count++;
            if (message.Role == ModelRole.User) break;
        }

        var group = new ModelMessage[count];
        var destination = 0;
        for (var index = start; index < end; index++)
        {
            var message = messages[index];
            if (message.Role != ModelRole.System) group[destination++] = message;
        }

        end = start;
        return group;
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
            Content = message.Continuation is null ? TrimContent(message.Content, share) : message.Content
        }).ToArray();
    }

    private static int Estimate(ModelMessage message) =>
        (message.Continuation is null ? message.Content.Length : 0) + EstimateMetadata(message);

    private static int EstimateMetadata(ModelMessage message) =>
        32 + (message.ToolCallId?.Length ?? 0) + (message.ToolName?.Length ?? 0)
        + (message.Continuation is { } continuation ? continuation.CharacterCount + message.Content.Length : 0)
        + (message.ToolCalls?.Sum(static call =>
            call.CallId.Length + call.Name.Length + call.Arguments.GetRawText().Length
            + (call.ProviderCallId?.Length ?? 0) + 32) ?? 0);

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
        var suffixStart = value.Length - (retained - prefixLength);
        if (prefixLength > 0 && char.IsHighSurrogate(value[prefixLength - 1]) && char.IsLowSurrogate(value[prefixLength]))
            prefixLength--;
        if (suffixStart > 0 && char.IsLowSurrogate(value[suffixStart]) && char.IsHighSurrogate(value[suffixStart - 1]))
            suffixStart++;
        return value[..prefixLength] + TruncationMarker + value[suffixStart..];
    }
}
