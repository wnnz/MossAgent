using System.Collections.Specialized;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerSearchReconciliationTests
{
    [Theory]
    [InlineData("target", "  TARGET  ")]
    [InlineData("target", "unique")]
    public async Task EquivalentPartialResults_RetainTheirGroupWithoutCollectionChanges(string firstQuery, string secondQuery)
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target", DisplayName = "unique" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.SearchQuery = firstQuery;
        var group = Assert.Single(picker.FilteredGroups);
        var row = Assert.Single(group.Items);
        picker.SelectItemCommand.Execute(row);
        var actions = Observe(picker);

        picker.SearchQuery = secondQuery;

        Assert.Same(group, Assert.Single(picker.FilteredGroups));
        Assert.Empty(actions);
        Assert.Same(row, picker.SelectedItem);
        Assert.True(row.IsSelected);
        Assert.True(picker.CanUseSelectedModel);
    }

    [Theory]
    [InlineData("", "   ")]
    [InlineData("fixture", "FIXTURE")]
    [InlineData("fixture", "model")]
    public async Task EquivalentWholeResults_DoNotResetTheCollection(string firstQuery, string secondQuery)
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.SearchQuery = firstQuery;
        var group = Assert.Single(picker.FilteredGroups);
        var actions = Observe(picker);

        picker.SearchQuery = secondQuery;

        Assert.Same(group, Assert.Single(picker.FilteredGroups));
        Assert.Empty(actions);
    }

    [Fact]
    public async Task NarrowingAndRestoringProviders_PublishOnlyTheChangedGroups()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var second = provider with { Id = Guid.NewGuid(), Name = "second", IsDefault = false };
        repository.Providers.Add(second);
        repository.Models.Add(model with { Id = Guid.NewGuid(), ProviderId = second.Id, DisplayName = "Other" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var groups = picker.FilteredGroups.ToArray();
        var selected = picker.SelectedItem;
        var actions = Observe(picker);

        picker.SearchQuery = "second";

        Assert.Equal([NotifyCollectionChangedAction.Remove], actions);
        Assert.Same(groups[1], Assert.Single(picker.FilteredGroups));
        actions.Clear();
        picker.SearchQuery = string.Empty;
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
        Assert.Equal(groups, picker.FilteredGroups);
        Assert.Same(selected, picker.SelectedItem);
    }

    [Fact]
    public async Task DifferentRowsWithTheSameCount_DoNotReuseAnEarlierPartialResult()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target-first", DisplayName = "First" });
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target-last", DisplayName = "Last" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var original = Assert.Single(picker.FilteredGroups);
        picker.SearchQuery = "first";
        var first = Assert.Single(picker.FilteredGroups);

        picker.SearchQuery = "last";

        var last = Assert.Single(picker.FilteredGroups);
        Assert.NotSame(first, last);
        Assert.Same(original.Items[2], Assert.Single(last.Items));
        Assert.Same(original.Items[1], Assert.Single(first.Items));
        var membership = Assert.IsAssignableFrom<IList<ModelPickerItemViewModel>>(last.Items);
        Assert.True(membership.IsReadOnly);
        Assert.Throws<NotSupportedException>(membership.Clear);
    }

    [Fact]
    public async Task ReloadWithTheSameQuery_ReplacesStaleRowsAndRejectsRemovedSelections()
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        var target = model with { Id = Guid.NewGuid(), ModelId = "target", DisplayName = "Target" };
        repository.Models.Add(target);
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        picker.SearchQuery = "target";
        var previous = Assert.Single(picker.FilteredGroups);
        var stale = Assert.Single(previous.Items);
        picker.SelectItemCommand.Execute(stale);
        repository.Models[1] = target with { ContextLength = 64000 };

        await picker.ReloadAsync();

        var updated = Assert.Single(Assert.Single(picker.FilteredGroups).Items);
        Assert.NotSame(previous, Assert.Single(picker.FilteredGroups));
        Assert.NotSame(stale, updated);
        Assert.Equal(64000, updated.Model.ContextLength);
        Assert.Same(updated, picker.SelectedItem);
        repository.Models[1] = target with { IsEnabled = false };
        await picker.ReloadAsync();
        Assert.Empty(picker.FilteredGroups);
        Assert.Null(picker.SelectedItem);
        Assert.False(picker.SelectItemCommand.CanExecute(stale));
        Assert.False(picker.CanUseSelectedModel);
    }

    [Fact]
    public async Task RepeatedEmptyResults_DoNotPublishResetsOrChangeTheSelection()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var selected = picker.SelectedItem;
        picker.SearchQuery = "missing";
        var actions = Observe(picker);

        picker.SearchQuery = "still missing";

        Assert.Empty(actions);
        Assert.Empty(picker.FilteredGroups);
        Assert.True(picker.HasNoMatches);
        Assert.False(picker.HasNoModels);
        Assert.Same(selected, picker.SelectedItem);
    }

    private static List<NotifyCollectionChangedAction> Observe(ModelPickerViewModel picker)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        picker.FilteredGroups.CollectionChanged += (_, args) => actions.Add(args.Action);
        return actions;
    }
}
