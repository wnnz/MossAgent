using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class SqliteTestPoolTests
{
    [Fact]
    public void CleanupRetiresOnlyOwnedIdleConnections()
    {
        var ownedRoot = CreateRoot();
        var otherRoot = CreateRoot();
        try
        {
            using var owned = CreateConnection(ownedRoot);
            using var other = CreateConnection(otherRoot);
            owned.Open();
            other.Open();
            var ownedHandle = owned.Handle!;
            var otherHandle = other.Handle!;
            owned.Close();
            other.Close();

            SqliteTestPool.Clear(ownedRoot);

            Assert.True(ownedHandle.IsClosed);
            Assert.False(otherHandle.IsClosed);
            other.Open();
            Assert.Same(otherHandle, other.Handle);
            Assert.Equal(42L, Scalar(other));
        }
        finally { CleanRoots(ownedRoot, otherRoot); }
    }

    [Fact]
    public void CleanupPreservesAnotherOpenConnection()
    {
        var ownedRoot = CreateRoot();
        var otherRoot = CreateRoot();
        try
        {
            using var other = CreateConnection(otherRoot);
            other.Open();
            var otherHandle = other.Handle!;

            SqliteTestPool.Clear(ownedRoot);
            SqliteTestPool.Clear(ownedRoot);

            Assert.Equal(ConnectionState.Open, other.State);
            Assert.False(otherHandle.IsClosed);
            Assert.Same(otherHandle, other.Handle);
            Assert.Equal(42L, Scalar(other));
        }
        finally { CleanRoots(ownedRoot, otherRoot); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void CleanupRejectsMissingRootWithoutTouchingExistingPool(string? missingRoot)
    {
        var root = CreateRoot();
        try
        {
            using var connection = CreateConnection(root);
            connection.Open();
            var handle = connection.Handle!;
            connection.Close();

            Assert.ThrowsAny<ArgumentException>(() => SqliteTestPool.Clear(missingRoot));

            Assert.False(handle.IsClosed);
            connection.Open();
            Assert.Same(handle, connection.Handle);
            Assert.Equal(42L, Scalar(connection));
        }
        finally { CleanRoots(root); }
    }

    [Fact]
    public void CleanupRetiresOwnedActiveConnectionOnlyAfterClose()
    {
        var root = CreateRoot();
        try
        {
            using var connection = CreateConnection(root);
            connection.Open();
            var handle = connection.Handle!;

            SqliteTestPool.Clear(root);

            Assert.False(handle.IsClosed);
            Assert.Equal(42L, Scalar(connection));
            connection.Close();
            Assert.True(handle.IsClosed);
            connection.Open();
            Assert.NotSame(handle, connection.Handle);
            Assert.Equal(42L, Scalar(connection));
        }
        finally { CleanRoots(root); }
    }

    [Fact]
    public void InfrastructureTestsDoNotClearProcessWidePools()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MossAgent.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        var tests = Path.Combine(directory.FullName, "tests", "MossAgent.Infrastructure.Tests");
        var pattern = new Regex(@"\bSqliteConnection\s*\.\s*ClearAllPools\s*\(", RegexOptions.CultureInvariant);
        var offenders = Directory.EnumerateFiles(tests, "*.cs")
            .Where(path => pattern.IsMatch(File.ReadAllText(path))).Select(Path.GetFileName).ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public async Task FailureFixtureDisposalPreservesAnotherFixtureAndItsHistory()
    {
        await using var other = await SqliteFailurePersistenceFixture.CreateAsync();
        await using var owned = await SqliteFailurePersistenceFixture.CreateAsync();
        using var connection = other.Connections.Create();
        await connection.OpenAsync(other.Token);
        var handle = connection.Handle!;
        connection.Close();

        await owned.DisposeAsync();

        Assert.False(handle.IsClosed);
        await other.Repository.SetTaskArchivedAsync(other.Task.Id, true, other.Token);
        Assert.Equal(other.Task, Assert.Single(await other.Repository.GetArchivedTasksAsync(other.Project.Id, other.Token)));
        await connection.OpenAsync(other.Token);
        Assert.Same(handle, connection.Handle);
    }

    private static SqliteConnection CreateConnection(string root) =>
        new SqliteConnectionFactory(new AppDataPaths(root)).Create();

    private static long Scalar(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 42;";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static string CreateRoot()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanRoots(params string[] roots)
    {
        foreach (var root in roots)
        {
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }
}
