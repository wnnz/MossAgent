using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerGroupFilteringTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void QueryTransitions_PreserveDuplicateOrderAndEarlierSnapshots(bool duplicateRows)
    {
        var (_, provider, model) = ModelPickerReloadTests.Profiles();
        var alpha = new ModelPickerItemViewModel(provider, model with { ModelId = "target-alpha", DisplayName = "Alpha" });
        var beta = new ModelPickerItemViewModel(provider, model with { ModelId = "target-beta", DisplayName = "Beta" });
        var other = new ModelPickerItemViewModel(provider, model with { ModelId = "other", DisplayName = "Other" });
        ModelPickerItemViewModel[] rows = duplicateRows ? [alpha, alpha, beta, other, beta] : [alpha, beta, other];
        var group = new ModelPickerGroupViewModel(provider, rows);
        string[] queries = ["alpha", "target", "beta", "other", "missing", "fixture", "", "ALPHA", "target-beta"];
        var snapshots = new List<(ModelPickerGroupViewModel Group, ModelPickerItemViewModel[] Rows)>();

        foreach (var previous in queries)
        {
            group.Filter(previous);
            foreach (var query in queries)
            {
                var whole = query.Length == 0 || provider.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
                var expected = whole ? rows : rows.Where(row => row.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || row.ModelId.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
                var filtered = group.Filter(query);
                if (expected.Length == 0) Assert.Null(filtered);
                else
                {
                    Assert.NotNull(filtered);
                    Assert.Equal(expected, filtered.Items);
                    if (expected.Length == rows.Length) Assert.Same(group, filtered);
                    snapshots.Add((filtered, filtered.Items.ToArray()));
                }
            }
        }

        foreach (var snapshot in snapshots)
        {
            Assert.Equal(snapshot.Rows, snapshot.Group.Items);
            var membership = Assert.IsAssignableFrom<IList<ModelPickerItemViewModel>>(snapshot.Group.Items);
            Assert.True(membership.IsReadOnly);
            Assert.Throws<NotSupportedException>(membership.Clear);
        }
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("fixture", true)]
    [InlineData("missing", false)]
    public void EmptyGroup_RetainsWholeProviderMatchSemantics(string query, bool whole)
    {
        var (_, provider, _) = ModelPickerReloadTests.Profiles();
        var group = new ModelPickerGroupViewModel(provider, []);

        Assert.Same(whole ? group : null, group.Filter(query));
        Assert.Empty(group.Items);
    }
}
