using MossAgent.Application.Models;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

internal sealed class ModelReasoningOptions
{
    private ProviderProtocol? _protocol;
    private string? _modelId;
    private IReadOnlyList<string> _choices = [];

    public IReadOnlyList<string> ForEditor(ProviderProtocol? protocol, string modelId, string selected)
    {
        if (_protocol == protocol && _modelId == modelId
            && (string.IsNullOrEmpty(selected) || _choices.Contains(selected, StringComparer.Ordinal))) return _choices;
        var supported = ModelReasoningPolicy.SupportedEfforts(protocol, modelId);
        _protocol = protocol;
        _modelId = modelId;
        _choices = string.IsNullOrEmpty(selected) || supported.Contains(selected, StringComparer.Ordinal)
            ? supported : [.. supported, selected];
        return _choices;
    }
}
