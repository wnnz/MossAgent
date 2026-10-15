using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ModelPickerSearchAllocationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("target", "unique")]
    [InlineData("shared", "model")]
    [InlineData("absent", "missing")]
    [InlineData("fixture", "FIXTURE")]
    public async Task EquivalentSearchResults_AvoidTemporaryCatalogSnapshots(string firstQuery, string secondQuery)
    {
        using var picker = new ModelPickerViewModel(CreateRepository());
        await picker.ReloadAsync();
        for (var iteration = 0; iteration < 100; iteration++)
            picker.SearchQuery = iteration % 2 == 0 ? firstQuery : secondQuery;
        var groups = picker.FilteredGroups.ToArray();
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
            picker.SearchQuery = iteration % 2 == 0 ? firstQuery : secondQuery;

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Picker searches ({firstQuery}/{secondQuery}, 1,000): {allocated} bytes");
        Assert.Equal(groups, picker.FilteredGroups);
        Assert.InRange(allocated, 0, 192_000);
    }

    [Theory]
    [InlineData("target", "unique")]
    [InlineData("shared", "model")]
    [InlineData("absent", "missing")]
    [InlineData("fixture", "FIXTURE")]
    public void EquivalentGroupResults_DoNotAllocate(string firstQuery, string secondQuery)
    {
        var repository = CreateRepository();
        var provider = repository.Providers[0];
        var group = new ModelPickerGroupViewModel(provider, repository.Models
            .Where(model => model.ProviderId == provider.Id)
            .Select(model => new ModelPickerItemViewModel(provider, model)));
        for (var iteration = 0; iteration < 100; iteration++)
            group.Filter(iteration % 2 == 0 ? firstQuery : secondQuery);
        var expected = group.Filter(secondQuery);
        var unchanged = true;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var iteration = 0; iteration < 1_000; iteration++)
            unchanged &= ReferenceEquals(expected, group.Filter(iteration % 2 == 0 ? firstQuery : secondQuery));

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Group searches ({firstQuery}/{secondQuery}, 1,000): {allocated} bytes");
        Assert.True(unchanged);
        Assert.Equal(0, allocated);
    }

    private static ModelPickerProbeRepository CreateRepository()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        repository.Providers.Clear();
        repository.Models.Clear();
        for (var providerIndex = 0; providerIndex < 12; providerIndex++)
        {
            var current = provider with { Id = Guid.NewGuid(), Name = $"fixture-{providerIndex}" };
            repository.Providers.Add(current);
            for (var modelIndex = 0; modelIndex < 32; modelIndex++)
                repository.Models.Add(model with
                {
                    Id = Guid.NewGuid(),
                    ProviderId = current.Id,
                    ModelId = $"shared-model-{(modelIndex % 4 == 0 ? "target" : "other")}-{modelIndex}",
                    DisplayName = $"shared {(modelIndex % 4 == 0 ? "unique" : "other")} {modelIndex}"
                });
        }
        return repository;
    }
}
