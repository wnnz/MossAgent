using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerGroupSnapshotTests
{
    [Fact]
    public void GroupMembership_IsImmutableAndDetachedFromTheInputCollection()
    {
        var (_, provider, model) = ModelPickerReloadTests.Profiles();
        var original = new ModelPickerItemViewModel(provider, model);
        var replacement = new ModelPickerItemViewModel(provider, model with { Id = Guid.NewGuid() });
        var source = new[] { original };
        var group = new ModelPickerGroupViewModel(provider, source);

        source[0] = replacement;

        Assert.Same(original, Assert.Single(group.Items));
        var list = Assert.IsAssignableFrom<IList<ModelPickerItemViewModel>>(group.Items);
        Assert.True(list.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => list[0] = replacement);
        Assert.Throws<NotSupportedException>(() => list.Add(replacement));
        Assert.Throws<NotSupportedException>(list.Clear);
        Assert.Same(original, Assert.Single(group.Items));
    }

    [Fact]
    public void ImmutableMembership_RetainsObservableRowSelection()
    {
        var (_, provider, model) = ModelPickerReloadTests.Profiles();
        var row = new ModelPickerItemViewModel(provider, model);
        var group = new ModelPickerGroupViewModel(provider, [row]);
        var changes = new List<string?>();
        group.Items[0].PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        row.IsSelected = true;

        Assert.True(group.Items[0].IsSelected);
        Assert.Equal([nameof(ModelPickerItemViewModel.IsSelected)], changes);
    }
}
