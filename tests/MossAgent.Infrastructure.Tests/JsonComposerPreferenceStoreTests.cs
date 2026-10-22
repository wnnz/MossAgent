using MossAgent.Application.Settings;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class JsonComposerPreferenceStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("MossAgent-composer-preferences-").FullName;
    private string FilePath => Path.Combine(_root, "composer-settings.json");
    private JsonComposerPreferenceStore CreateStore() => new(new AppDataPaths(_root));

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task MissingFileDefaultsToEnterAndRoundTripSurvivesRestart()
    {
        var token = TestContext.Current.CancellationToken;
        Assert.Equal(ComposerSendMode.EnterSends, await CreateStore().LoadAsync(token));

        await CreateStore().SaveAsync(ComposerSendMode.ControlEnterSends, token);

        Assert.Equal(ComposerSendMode.ControlEnterSends, await CreateStore().LoadAsync(token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }

    [Fact]
    public async Task SaveKeepsUnknownSettingsAndOtherFilesSeparate()
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(FilePath,
            "{\"ComposerSendMode\":\"EnterSends\",\"Future\":\"keep\"}", token);
        await File.WriteAllTextAsync(Path.Combine(_root, "sidebar-settings.json"), "sidebar sentinel", token);

        await CreateStore().SaveAsync(ComposerSendMode.ControlEnterSends, token);

        Assert.Contains("\"Future\":\"keep\"", await File.ReadAllTextAsync(FilePath, token));
        Assert.Equal("sidebar sentinel",
            await File.ReadAllTextAsync(Path.Combine(_root, "sidebar-settings.json"), token));
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"ComposerSendMode\":99}")]
    [InlineData("{\"ComposerSendMode\":\"invalid\"}")]
    public async Task CorruptConfigurationIsRejectedWithoutOverwrite(string content)
    {
        var token = TestContext.Current.CancellationToken;
        await File.WriteAllTextAsync(FilePath, content, token);
        var store = CreateStore();

        await Assert.ThrowsAnyAsync<Exception>(() => store.LoadAsync(token));
        await Assert.ThrowsAnyAsync<Exception>(() => store.SaveAsync(ComposerSendMode.EnterSends, token));

        Assert.Equal(content, await File.ReadAllTextAsync(FilePath, token));
        Assert.Empty(Directory.GetFiles(_root, "*.tmp"));
    }
}
