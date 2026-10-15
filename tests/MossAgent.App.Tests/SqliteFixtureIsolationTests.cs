using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SqliteFixtureIsolationTests
{
    [Theory]
    [InlineData("command", false)]
    [InlineData("command", true)]
    [InlineData("history", false)]
    [InlineData("history", true)]
    public async Task FixtureDisposalPreservesAnotherDatabasePool(string fixtureKind, bool keepOpen)
    {
        var root = Directory.CreateTempSubdirectory("MossAgent-app-sqlite-").FullName;
        try
        {
            using var connection = CreateConnection(root);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var handle = connection.Handle!;
            if (!keepOpen) connection.Close();

            await CreateAndDisposeFixtureAsync(fixtureKind);

            if (keepOpen)
            {
                Assert.Equal(ConnectionState.Open, connection.State);
                Assert.Equal(42L, Scalar(connection));
                connection.Close();
            }
            Assert.False(handle.IsClosed);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            Assert.Same(handle, connection.Handle);
            Assert.Equal(42L, Scalar(connection));
        }
        finally
        {
            using var connection = CreateConnection(root);
            SqliteConnection.ClearPool(connection);
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void AppAndSharedTestsDoNotClearProcessWidePools()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var pattern = new Regex(@"\bClearAllPools\s*\(", RegexOptions.CultureInvariant);
        var paths = new[] { "MossAgent.App.Tests", "Shared" }
            .Select(name => Path.Combine(directory.FullName, "tests", name))
            .Where(Directory.Exists)
            .SelectMany(path => Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories))
            .Where(path => !Path.GetRelativePath(directory.FullName, path).Split(Path.DirectorySeparatorChar)
                .Any(segment => segment is "bin" or "obj"));
        var offenders = paths.Where(path => pattern.IsMatch(File.ReadAllText(path)))
            .Select(Path.GetFileName).ToArray();
        Assert.Empty(offenders);
    }

    private static async Task CreateAndDisposeFixtureAsync(string fixtureKind)
    {
        await using IAsyncDisposable fixture = fixtureKind == "command"
            ? await WorkspaceCommandFixture.CreateAsync("exit 0")
            : await WorkspaceHistoryFlowFixture.CreateAsync(mutation: false);
    }

    private static SqliteConnection CreateConnection(string root) =>
        new SqliteConnectionFactory(new AppDataPaths(root)).Create();

    private static long Scalar(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 42;";
        return Convert.ToInt64(command.ExecuteScalar());
    }
}
