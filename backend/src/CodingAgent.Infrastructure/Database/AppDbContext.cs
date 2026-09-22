using CodingAgent.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Database;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AuthRecord> AuthRecords => Set<AuthRecord>();
    public DbSet<Provider> Providers => Set<Provider>();
    public DbSet<ProviderModel> ProviderModels => Set<ProviderModel>();
    public DbSet<ProxyServer> Proxies => Set<ProxyServer>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<SubAgent> SubAgents => Set<SubAgent>();
    public DbSet<MemoryItem> Memories => Set<MemoryItem>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<McpServer> McpServers => Set<McpServer>();
    public DbSet<McpTool> McpTools => Set<McpTool>();
    public DbSet<AppSetting> Settings => Set<AppSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 枚举属性默认按 int 存储，无需额外转换
        modelBuilder.Entity<AuthRecord>().ToTable("auth_record");
        modelBuilder.Entity<Provider>().ToTable("providers");
        modelBuilder.Entity<ProviderModel>().ToTable("provider_models");
        modelBuilder.Entity<ProxyServer>().ToTable("proxies");
        modelBuilder.Entity<Session>().ToTable("sessions");
        modelBuilder.Entity<ChatMessage>().ToTable("messages");
        modelBuilder.Entity<SubAgent>().ToTable("sub_agents");
        modelBuilder.Entity<MemoryItem>().ToTable("memories");
        modelBuilder.Entity<Skill>().ToTable("skills");
        modelBuilder.Entity<McpServer>().ToTable("mcp_servers");
        modelBuilder.Entity<McpTool>().ToTable("mcp_tools");
        modelBuilder.Entity<AppSetting>().ToTable("settings");

        modelBuilder.Entity<ProviderModel>()
            .HasIndex(m => new { m.ProviderId, m.ModelId })
            .IsUnique();
        modelBuilder.Entity<ChatMessage>()
            .HasIndex(m => m.SessionId);
        modelBuilder.Entity<Skill>()
            .HasIndex(s => s.Name)
            .IsUnique();
        modelBuilder.Entity<AppSetting>()
            .HasKey(s => s.Key);
    }
}
