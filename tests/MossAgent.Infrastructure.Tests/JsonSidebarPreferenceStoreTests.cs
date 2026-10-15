using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class JsonSidebarPreferenceStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("MossAgent-sidebar-preferences-").FullName;
    private string FilePath => Path.Combine(_root, "sidebar-settings.json");
    private JsonSidebarPreferenceStore CreateStore() => new(new AppDataPaths(_root));
    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task RoundTrip_RestoresManualOrderAndKeepsOtherSettingsSeparate()
    {
        var token = TestContext.Current.CancellationToken;
        var store = CreateStore();
        Assert.Equal(SidebarProjectSortOrder.UpdatedAt, (await store.LoadAsync(token)).ProjectSortOrder);
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var preference = new SidebarPreference(SidebarProjectSortOrder.Manual, ids);
        await File.WriteAllTextAsync(Path.Combine(_root, "ui-settings.json"), "theme sentinel", token);
        await store.SaveAsync(preference, token);
        var loaded = await CreateStore().LoadAsync(token);
        Assert.Equal(preference.ProjectSortOrder, loaded.ProjectSortOrder);
        Assert.Equal(ids, loaded.ManualProjectOrder);
        Assert.Equal("theme sentinel", await File.ReadAllTextAsync(Path.Combine(_root, "ui-settings.json"), token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"ProjectSortOrder\":\"invalid\",\"ManualProjectOrder\":[]}")]
    [InlineData("{\"ProjectSortOrder\":\"Manual\",\"ManualProjectOrder\":[\"not-an-id\"]}")]
    [InlineData("{\"ProjectSortOrder\":\"Manual\",\"ManualProjectOrder\":[\"00000000-0000-0000-0000-000000000000\"]}")]
    public async Task CorruptPreferences_RejectReadsAndWritesWithoutOverwriting(string content)
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(FilePath, content, token);
        var store = CreateStore();
        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync(token));
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(new(SidebarProjectSortOrder.Manual, []), token));
        Assert.Equal(content, await File.ReadAllTextAsync(FilePath, token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task UnknownKeys_SurviveSavedChanges()
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(FilePath,
            "{\"ProjectSortOrder\":\"UpdatedAt\",\"ManualProjectOrder\":[],\"Future\":\"keep\"}", token);
        await CreateStore().SaveAsync(new(SidebarProjectSortOrder.Manual, []), token);
        Assert.Contains("\"Future\":\"keep\"", await File.ReadAllTextAsync(FilePath, token));
    }

    [Fact]
    public async Task InvalidInput_RejectsDuplicateIdsBeforeWriting()
    {
        var token = TestContext.Current.CancellationToken;
        var id = Guid.NewGuid();
        var store = CreateStore();
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new(SidebarProjectSortOrder.Manual, [id, id]), token));
        await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(new((SidebarProjectSortOrder)99, []), token));
        Assert.False(File.Exists(FilePath));
    }

    [Fact]
    public async Task CancelledWrite_DoesNotCreateOrReplaceTheSettingsFile()
    {
        var token = TestContext.Current.CancellationToken;
        var store = CreateStore();
        await store.SaveAsync(new(SidebarProjectSortOrder.UpdatedAt, []), token);
        var previous = await File.ReadAllTextAsync(FilePath, token);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(new(SidebarProjectSortOrder.Manual, []), cancellation.Token));
        Assert.Equal(previous, await File.ReadAllTextAsync(FilePath, token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task ConcurrentWrites_ProduceOneCompleteSnapshot()
    {
        var token = TestContext.Current.CancellationToken;
        var store = CreateStore();
        var tasks = Enumerable.Range(0, 8).Select(_ => store.SaveAsync(
            new SidebarPreference(SidebarProjectSortOrder.Manual, [Guid.NewGuid(), Guid.NewGuid()]), token));
        await Task.WhenAll(tasks);
        Assert.Equal(2, (await store.LoadAsync(token)).ManualProjectOrder.Count);
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
}
