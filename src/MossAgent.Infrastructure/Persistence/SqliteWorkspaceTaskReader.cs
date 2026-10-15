using Microsoft.Data.Sqlite;
using MossAgent.Application.Agent;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

internal static class SqliteWorkspaceTaskReader
{
    public static async Task<IReadOnlyList<AgentTask>> ReadAsync(SqliteCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var results = new List<AgentTask>();
        while (await reader.ReadAsync(cancellationToken)) results.Add(Read(reader));
        return results;
    }

    public static AgentTask Read(SqliteDataReader reader) => new(
            Guid.Parse(reader.GetString(0)), reader.IsDBNull(1) ? null : Guid.Parse(reader.GetString(1)), reader.GetString(2),
        (ApprovalPolicy)reader.GetInt32(3), (AgentTaskStatus)reader.GetInt32(4),
        DateTimeOffset.Parse(reader.GetString(5)), DateTimeOffset.Parse(reader.GetString(6)),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.GetInt32(8) != 0,
        TaskFailureClassifier.Normalize((AgentTaskStatus)reader.GetInt32(4),
            reader.IsDBNull(9) ? null : (TaskFailureReason)reader.GetInt32(9)));
}
