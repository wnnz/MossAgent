using Microsoft.Data.Sqlite;

namespace MossAgent.Infrastructure.Persistence;

internal static class WorktreeIntentSqlite
{
    public static async Task<int> UpdateAsync(SqliteConnectionFactory connections, string commandText,
        CancellationToken token, params (string Name, object? Value)[] parameters)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return await command.ExecuteNonQueryAsync(token);
    }

    public static async Task<T> QueryScalarAsync<T>(SqliteConnectionFactory connections, string commandText,
        CancellationToken token, params (string Name, object? Value)[] parameters)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var result = await command.ExecuteScalarAsync(token);
        return result is null or DBNull ? default! : (T)Convert.ChangeType(result, typeof(T));
    }

    public static async Task<IReadOnlyList<T>> QueryAsync<T>(SqliteConnectionFactory connections,
        string commandText, Func<SqliteDataReader, T> read, CancellationToken token,
        params (string Name, object? Value)[] parameters)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        var items = new List<T>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
            items.Add(read(reader));
        return items;
    }
}
