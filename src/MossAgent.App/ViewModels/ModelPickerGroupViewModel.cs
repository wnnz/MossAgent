using System.Collections.Immutable;
using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

/// <summary>
/// 模型选择器分组视图模型，按 AI 供应商聚合所属模型列表。
/// </summary>
public sealed class ModelPickerGroupViewModel
{
    private ModelPickerGroupViewModel? _filteredGroup;

    public ModelPickerGroupViewModel(AiProvider provider, IEnumerable<ModelPickerItemViewModel> items)
    {
        Provider = provider;
        Items = items.ToImmutableArray();
    }

    /// <summary>
    /// 当前分组对应的 AI 供应商。
    /// </summary>
    public AiProvider Provider { get; }

    /// <summary>
    /// 供应商名称。
    /// </summary>
    public string ProviderName => Provider.Name;

    /// <summary>
    /// 该供应商下挂载的模型条目集合。
    /// </summary>
    public IReadOnlyList<ModelPickerItemViewModel> Items { get; }

    internal ModelPickerGroupViewModel? Filter(string query)
    {
        if (string.IsNullOrEmpty(query) || ProviderName.Contains(query, StringComparison.OrdinalIgnoreCase)) return this;
        var retained = _filteredGroup?.Items ?? Items;
        ImmutableArray<ModelPickerItemViewModel>.Builder? matches = null;
        var count = 0;
        for (var index = 0; index < Items.Count; index++)
        {
            var item = Items[index];
            if (!item.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                && !item.ModelId.Contains(query, StringComparison.OrdinalIgnoreCase)) continue;
            if (matches is null && (count >= retained.Count || !ReferenceEquals(retained[count], item)))
                matches = CopyPrefix(retained, count);
            matches?.Add(item);
            count++;
        }
        if (count == 0) return null;
        if (count == Items.Count) return this;
        if (matches is null && count == retained.Count) return _filteredGroup;
        matches ??= CopyPrefix(retained, count);
        return _filteredGroup = new ModelPickerGroupViewModel(Provider, matches.ToImmutable());
    }

    private static ImmutableArray<ModelPickerItemViewModel>.Builder CopyPrefix(
        IReadOnlyList<ModelPickerItemViewModel> items, int count)
    {
        var builder = ImmutableArray.CreateBuilder<ModelPickerItemViewModel>(count);
        for (var index = 0; index < count; index++) builder.Add(items[index]);
        return builder;
    }
}
