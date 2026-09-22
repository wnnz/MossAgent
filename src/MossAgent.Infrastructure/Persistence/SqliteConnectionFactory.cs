using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteConnectionFactory(AppDataPaths paths)
{
    public SqliteConnection Create()
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true
        };
        return new SqliteConnection(builder.ConnectionString);
    }
}

