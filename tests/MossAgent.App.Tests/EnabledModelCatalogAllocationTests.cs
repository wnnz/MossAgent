using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EnabledModelCatalogAllocationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task WarmDisabledCatalogReads_AvoidPerProviderFilterAllocations()
    {
        var (repository, provider, model) = ModelPickerReloadTests.Profiles();
        var providers = Enumerable.Range(0, 16)
            .Select(_ => provider with { Id = Guid.NewGuid() }).ToArray();
        var models = Enumerable.Range(0, 64)
            .Select(_ => model with { Id = Guid.NewGuid(), IsEnabled = false }).ToArray();
        var providerRead = Task.FromResult<IReadOnlyList<AiProvider>>(providers);
        var modelRead = Task.FromResult<IReadOnlyList<ModelProfile>>(models);
        repository.ProviderLoader = () => providerRead;
        repository.ModelLoader = _ => modelRead;
        var catalog = new EnabledModelCatalog(repository);
        var token = TestContext.Current.CancellationToken;
        for (var index = 0; index < 100; index++) await catalog.ReadAsync(token);
        var valid = true;
        var before = GC.GetAllocatedBytesForCurrentThread();

        for (var index = 0; index < 1_000; index++)
            valid &= (await catalog.ReadAsync(token)).Count == 0;

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        output.WriteLine($"Disabled catalog reads (1,000): {allocated} bytes");
        Assert.True(valid);
        Assert.InRange(allocated, 0, 1_024_000);
    }
}
