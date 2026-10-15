using System.Collections.ObjectModel;

namespace MossAgent.App.ViewModels;

public sealed class AgentModelSelection
{
    private (Guid ProviderId, Guid ModelId)? _preferredModel;
    private bool _initialized;

    public bool HasPreferredModel => _preferredModel is not null;

    public bool Select(AgentModelOption? option)
    {
        var identity = option is null ? ((Guid, Guid)?)null : (option.Model.ProviderId, option.Model.Id);
        var changed = _preferredModel != identity;
        _preferredModel = identity;
        return changed;
    }

    public AgentModelOption? Restore(Guid providerId, Guid modelId, IEnumerable<AgentModelOption> options)
    {
        _preferredModel = (providerId, modelId);
        return Find(options);
    }

    public AgentModelOption? Publish(ObservableCollection<AgentModelOption> options,
        IReadOnlyList<AgentModelOption> ordered, bool allowInitialSelection)
    {
        if (!options.SequenceEqual(ordered)) options.ReplaceWith(ordered);
        var selected = HasPreferredModel ? Find(options)
            : !_initialized && allowInitialSelection ? options.FirstOrDefault() : null;
        if (!_initialized && selected is not null) Select(selected);
        _initialized |= ordered.Count > 0;
        return selected;
    }

    private AgentModelOption? Find(IEnumerable<AgentModelOption> options) => _preferredModel is { } identity
        ? options.FirstOrDefault(option => option.Model.ProviderId == identity.ProviderId && option.Model.Id == identity.ModelId)
        : null;
}
