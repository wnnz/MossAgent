using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerSearchTests
{
    [Theory]
    [InlineData("", 2)]
    [InlineData("   ", 2)]
    [InlineData("  FIXTURE  ", 1)]
    [InlineData(" SECOND ", 1)]
    public async Task WholeProviderMatches_ReuseCatalogGroupsAndRows(string query, int expectedGroups)
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var second = provider with { Id = Guid.NewGuid(), Name = "second", IsDefault = false };
        repository.Providers.Add(second);
        repository.Models.Add(model with
        {
            Id = Guid.NewGuid(),
            ProviderId = second.Id,
            ModelId = "other",
            DisplayName = "Other model"
        });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var groups = picker.FilteredGroups.ToArray();
        var selected = picker.SelectedItem;

        picker.SearchQuery = query;

        Assert.Equal(expectedGroups, picker.FilteredGroups.Count);
        foreach (var group in picker.FilteredGroups)
        {
            var original = Assert.Single(groups, item => item.Provider.Id == group.Provider.Id);
            Assert.Same(original, group);
            Assert.Same(original.Items[0], group.Items[0]);
        }
        Assert.Same(selected, picker.SelectedItem);
        Assert.True(picker.CanUseSelectedModel);
    }

    [Theory]
    [InlineData("  TARGET  ")]
    [InlineData("  UNIQUE  ")]
    public async Task PartialMatches_PreserveCatalogOrderAndShareSelectionState(string query)
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target-first", DisplayName = "unique first" });
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "unmatched", DisplayName = "different" });
        repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = "target-last", DisplayName = "unique last" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var group = Assert.Single(picker.FilteredGroups);
        var first = group.Items[1];
        var last = group.Items[3];

        picker.SearchQuery = query;

        var filtered = Assert.Single(picker.FilteredGroups);
        Assert.NotSame(group, filtered);
        Assert.Collection(filtered.Items, item => Assert.Same(first, item), item => Assert.Same(last, item));
        picker.SelectItemCommand.Execute(last);
        Assert.Same(last, picker.SelectedItem);
        Assert.True(last.IsSelected);
        Assert.All(group.Items.Where(item => !ReferenceEquals(item, last)), item => Assert.False(item.IsSelected));
        picker.SearchQuery = string.Empty;
        Assert.Same(group, Assert.Single(picker.FilteredGroups));
        Assert.Same(last, picker.SelectedItem);
    }

    [Fact]
    public async Task NoMatches_DoesNotClearTheSelectionOrOriginalCatalog()
    {
        var (repository, _, _) = ModelPickerReloadTests.Profiles();
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();
        var group = Assert.Single(picker.FilteredGroups);
        var selected = picker.SelectedItem;

        picker.SearchQuery = "missing";

        Assert.Empty(picker.FilteredGroups);
        Assert.True(picker.HasNoMatches);
        Assert.True(picker.HasModels);
        Assert.False(picker.HasNoModels);
        Assert.Same(selected, picker.SelectedItem);
        picker.SearchQuery = string.Empty;
        Assert.Same(group, Assert.Single(picker.FilteredGroups));
        Assert.False(picker.HasNoMatches);
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public async Task SearchVisibility_UsesTheUnfilteredCatalog(int modelCount, bool expected)
    {
        var (repository, _, model) = ModelPickerReloadTests.Profiles();
        for (var index = 1; index < modelCount; index++)
            repository.Models.Add(model with { Id = Guid.NewGuid(), ModelId = $"model-{index}" });
        using var picker = new ModelPickerViewModel(repository);
        await picker.ReloadAsync();

        picker.SearchQuery = "missing";

        Assert.Equal(expected, picker.ShowSearch);
        Assert.False(picker.ShowProviderHeaders);
    }
}
