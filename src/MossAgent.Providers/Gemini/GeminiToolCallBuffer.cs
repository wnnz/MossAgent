using MossAgent.Application.Models;

namespace MossAgent.Providers.Gemini;

internal sealed class GeminiToolCallBuffer
{
    internal const int MaximumCalls = 128;
    internal const int MaximumArgumentCharacters = 1024 * 1024;
    internal const int MaximumTotalArgumentCharacters = 4 * MaximumArgumentCharacters;
    internal const int MaximumIdentityCharacters = 512;
    private readonly List<ToolCallCompleted> _calls = [];
    private int _argumentCharacters;

    public ModelFailed? Capture(ToolCallCompleted call)
    {
        if (_calls.Count >= MaximumCalls)
            return new ModelFailed("tool_calls_limit", ProviderText.Get("GeminiToolCallsLimit"));
        var length = call.Arguments.GetRawText().Length;
        if (length > MaximumTotalArgumentCharacters - _argumentCharacters)
            return new ModelFailed("tool_arguments_limit", ProviderText.Get("GeminiToolArgumentsLimit"));
        _calls.Add(call);
        _argumentCharacters += length;
        return null;
    }

    public IReadOnlyList<ModelEvent> Drain()
    {
        var result = _calls.Cast<ModelEvent>().ToArray();
        Clear();
        return result;
    }

    public void Clear()
    {
        _calls.Clear();
        _argumentCharacters = 0;
    }
}
