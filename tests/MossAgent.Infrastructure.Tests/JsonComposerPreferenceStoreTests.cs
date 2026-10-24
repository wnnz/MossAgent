using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class JsonComposerPreferenceStoreTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static AppDataPaths Paths() => new(Path.Combine(Path.GetTempPath(), "moss-composer-" + Guid.NewGuid().ToString("N")));
    private static string FileOf(AppDataPaths paths) => Path.Combine(paths.Root, "composer-settings.json");

    [Fact]
    public async Task MissingFileDefaultsToQueue()
    {
        var store = new JsonComposerPreferenceStore(Paths());
        Assert.Equal(ComposerRunningAction.Queue, await store.LoadAsync(Token));
    }

    [Theory]
    [InlineData(ComposerRunningAction.Queue)]
    [InlineData(ComposerRunningAction.Steer)]
    public async Task RoundTripPersistsAcrossStoreInstances(ComposerRunningAction action)
    {
        var paths = Paths();
        await new JsonComposerPreferenceStore(paths).SaveAsync(action, Token);
        Assert.Equal(action, await new JsonComposerPreferenceStore(paths).LoadAsync(Token));
    }

    [Fact]
    public async Task UnknownKeysAreRetainedAcrossSaves()
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        await File.WriteAllTextAsync(FileOf(paths), """{"DefaultRunningAction":"Queue","future-key":{"x":1}}""", Token);
        var store = new JsonComposerPreferenceStore(paths);
        Assert.Equal(ComposerRunningAction.Queue, await store.LoadAsync(Token));
        await store.SaveAsync(ComposerRunningAction.Steer, Token);
        var written = await File.ReadAllTextAsync(FileOf(paths), Token);
        Assert.Contains("future-key", written);
        Assert.Contains("Steer", written);
    }

    [Theory]
    [InlineData("""{"DefaultRunningAction":"Nope"}""")]
    [InlineData("""["Queue"]""")]
    [InlineData("""not json""")]
    [InlineData("""{"DefaultRunningAction":3}""")]
    public async Task CorruptOrUnknownContentRejectsLoadAndKeepsSource(string content)
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        await File.WriteAllTextAsync(FileOf(paths), content, Token);
        var store = new JsonComposerPreferenceStore(paths);
        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync(Token));
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(ComposerRunningAction.Steer, Token));
        Assert.Equal(content, await File.ReadAllTextAsync(FileOf(paths), Token));
    }

    [Fact]
    public async Task OversizedFileIsRejected()
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        await File.WriteAllTextAsync(FileOf(paths), new string('x', 17 * 1024), Token);
        await Assert.ThrowsAnyAsync<Exception>(() => new JsonComposerPreferenceStore(paths).LoadAsync(Token));
    }

    [Fact]
    public async Task InvalidEnumAndCancelledSaveWriteNothing()
    {
        var paths = Paths();
        var store = new JsonComposerPreferenceStore(paths);
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync((ComposerRunningAction)99, Token));
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => store.SaveAsync(ComposerRunningAction.Steer, cancelled.Token));
        Assert.False(File.Exists(FileOf(paths)));
        Assert.Empty(Directory.Exists(paths.Root)
            ? Directory.GetFiles(paths.Root, ".composer-settings-*.tmp") : Array.Empty<string>());
    }

    [Fact]
    public async Task ConcurrentSavesProduceOneValidSnapshot()
    {
        var paths = Paths();
        var store = new JsonComposerPreferenceStore(paths);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(index =>
            store.SaveAsync(index % 2 == 0 ? ComposerRunningAction.Queue : ComposerRunningAction.Steer, Token)));
        var loaded = await new JsonComposerPreferenceStore(paths).LoadAsync(Token);
        Assert.True(Enum.IsDefined(loaded));
    }

    [Fact]
    public async Task SentinelFilesOfOtherStoresAreUntouched()
    {
        var paths = Paths();
        Directory.CreateDirectory(paths.Root);
        var sentinel = Path.Combine(paths.Root, "sidebar-settings.json");
        await File.WriteAllTextAsync(sentinel, """{"sentinel":true}""", Token);
        var store = new JsonComposerPreferenceStore(paths);
        await store.SaveAsync(ComposerRunningAction.Steer, Token);
        Assert.Equal("""{"sentinel":true}""", await File.ReadAllTextAsync(sentinel, Token));
    }
}
