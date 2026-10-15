using Microsoft.Data.Sqlite;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteModelIdentityTests
{
    [Fact]
    public async Task ExistingIdentity_CanChangeApiModelIdWithoutCreatingAnotherRow()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = await SqliteModelIdentityFixture.CreateAsync(cancellationToken);
        var original = fixture.Model("before", true);
        await fixture.Repository.SaveModelAsync(original, cancellationToken);
        var edited = original with { ModelId = "after", DisplayName = "renamed-model", ContextLength = 256000 };

        await fixture.Repository.SaveModelAsync(edited, cancellationToken);

        var stored = Assert.Single(await fixture.Repository.GetModelsAsync(fixture.Provider.Id, cancellationToken));
        Assert.Equal(edited, stored);
    }

    [Fact]
    public async Task RenameToExistingApiModelId_RejectsAndRollsBackDefaultsAndBothRows()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = await SqliteModelIdentityFixture.CreateAsync(cancellationToken);
        var first = fixture.Model("first");
        var second = fixture.Model("second", true);
        await fixture.Repository.SaveModelAsync(first, cancellationToken);
        await fixture.Repository.SaveModelAsync(second, cancellationToken);

        await Assert.ThrowsAsync<SqliteException>(() => fixture.Repository.SaveModelAsync(
            first with { ModelId = second.ModelId, DisplayName = "must-not-merge", IsDefault = true }, cancellationToken));

        var stored = await fixture.Repository.GetModelsAsync(fixture.Provider.Id, cancellationToken);
        Assert.Equal(2, stored.Count);
        Assert.Contains(first, stored);
        Assert.Contains(second, stored);
        Assert.Equal(second.Id, Assert.Single(stored, model => model.IsDefault).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignProviderIdentity_RejectsWithoutChangingEitherProviderModels(bool existingApiId)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = await SqliteModelIdentityFixture.CreateAsync(cancellationToken);
        var original = fixture.Model("first", true);
        var otherProvider = SqliteModelIdentityFixture.CreateProvider("other-provider");
        await fixture.Repository.SaveProviderAsync(otherProvider, cancellationToken);
        var otherModel = fixture.Model("other", true) with { ProviderId = otherProvider.Id };
        await fixture.Repository.SaveModelAsync(original, cancellationToken);
        await fixture.Repository.SaveModelAsync(otherModel, cancellationToken);

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Repository.SaveModelAsync(
            original with { ProviderId = otherProvider.Id, ModelId = existingApiId ? otherModel.ModelId : "moved", IsDefault = true },
            cancellationToken));

        Assert.Equal(original, Assert.Single(await fixture.Repository.GetModelsAsync(fixture.Provider.Id, cancellationToken)));
        Assert.Equal(otherModel, Assert.Single(await fixture.Repository.GetModelsAsync(otherProvider.Id, cancellationToken)));
    }

    [Fact]
    public async Task NewIdentityWithExistingApiModelId_PreservesExistingSemanticUpsertIdentity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var fixture = await SqliteModelIdentityFixture.CreateAsync(cancellationToken);
        var original = fixture.Model("existing");
        await fixture.Repository.SaveModelAsync(original, cancellationToken);
        var imported = fixture.Model(original.ModelId, true) with { DisplayName = "updated", MaximumOutputTokens = 16384 };

        await fixture.Repository.SaveModelAsync(imported, cancellationToken);

        var stored = Assert.Single(await fixture.Repository.GetModelsAsync(fixture.Provider.Id, cancellationToken));
        Assert.Equal(imported with { Id = original.Id }, stored);
    }
}
