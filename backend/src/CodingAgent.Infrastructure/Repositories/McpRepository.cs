using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using CodingAgent.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Repositories;

public class McpRepository(AppDbContext db) : IMcpRepository
{
    public async Task<List<McpServer>> GetAllServersAsync(CancellationToken ct = default) =>
        await db.McpServers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(ct);

    public Task<McpServer?> GetServerAsync(int id, CancellationToken ct = default) =>
        db.McpServers.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task AddServerAsync(McpServer server, CancellationToken ct = default)
    {
        db.McpServers.Add(server);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateServerAsync(McpServer server, CancellationToken ct = default)
    {
        db.McpServers.Update(server);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteServerAsync(int id, CancellationToken ct = default)
    {
        var server = await db.McpServers.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (server is null) return;
        db.McpServers.Remove(server);
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<McpTool>> GetToolsAsync(int serverId, CancellationToken ct = default) =>
        await db.McpTools.AsNoTracking().Where(t => t.ServerId == serverId).OrderBy(t => t.Name).ToListAsync(ct);

    public async Task ReplaceToolsAsync(int serverId, IEnumerable<McpTool> tools, CancellationToken ct = default)
    {
        var existing = await db.McpTools.Where(t => t.ServerId == serverId).ToListAsync(ct);
        db.McpTools.RemoveRange(existing);
        db.McpTools.AddRange(tools);
        await db.SaveChangesAsync(ct);
    }
}
