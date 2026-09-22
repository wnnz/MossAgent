using MossAgent.Application.Persistence;
using MossAgent.Domain;

namespace MossAgent.Infrastructure.Persistence;

public sealed class SqliteBrowserConfigurationRepository(SqliteConnectionFactory connections)
    : IBrowserConfigurationRepository
{
    public async Task<BrowserConfiguration> GetAsync(CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT default_browser,profile_preference,profile_directory,cdp_endpoint,proxy_id
            FROM browser_configuration WHERE singleton_id=1;
            """;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return new BrowserConfiguration(
                DefaultBrowserKind.Embedded, BrowserProfilePreference.Managed, null, null, null);
        }

        var endpoint = reader.IsDBNull(3) ? null : new Uri(reader.GetString(3));
        Guid? proxyId = reader.IsDBNull(4) ? null : Guid.Parse(reader.GetString(4));
        return new BrowserConfiguration(
            (DefaultBrowserKind)reader.GetInt32(0),
            (BrowserProfilePreference)reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetString(2), endpoint, proxyId);
    }

    public async Task SaveAsync(
        BrowserConfiguration configuration,
        CancellationToken cancellationToken)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO browser_configuration(singleton_id,default_browser,profile_preference,
                profile_directory,cdp_endpoint,proxy_id)
            VALUES(1,$browser,$profile,$directory,$cdp,$proxy)
            ON CONFLICT(singleton_id) DO UPDATE SET default_browser=$browser,
                profile_preference=$profile,profile_directory=$directory,
                cdp_endpoint=$cdp,proxy_id=$proxy;
            """;
        command.Parameters.AddWithValue("$browser", (int)configuration.DefaultBrowser);
        command.Parameters.AddWithValue("$profile", (int)configuration.ProfilePreference);
        command.Parameters.AddWithValue("$directory", (object?)configuration.ProfileDirectory ?? DBNull.Value);
        command.Parameters.AddWithValue("$cdp", (object?)configuration.CdpEndpoint?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("$proxy", (object?)configuration.ProxyId?.ToString() ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
