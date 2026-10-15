using Microsoft.Data.Sqlite;
using MossAgent.Infrastructure.Persistence;

namespace MossAgent.Testing;

internal static class SqliteTestPool
{
    public static void Clear(string? root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        using var connection = new SqliteConnectionFactory(new AppDataPaths(root)).Create();
        SqliteConnection.ClearPool(connection);
    }
}
