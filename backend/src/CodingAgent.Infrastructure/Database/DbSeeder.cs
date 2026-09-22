using CodingAgent.Domain.Entities;
using CodingAgent.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CodingAgent.Infrastructure.Database;

/// <summary>启动时建库并种入默认设置。</summary>
public static class DbSeeder
{
    public const string WorkspacePathKey = "workspace_path";
    public const string MaxTurnsKey = "max_turns";
    public const string JwtSecretKey = "jwt_secret";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher? _ = null, CancellationToken ct = default)
    {
        await db.Database.EnsureCreatedAsync(ct);

        var settings = db.Settings;
        if (!await settings.AnyAsync(ct))
        {
            settings.AddRange(
                new AppSetting { Key = WorkspacePathKey, Value = string.Empty },
                new AppSetting { Key = MaxTurnsKey, Value = "25" });
        }

        // JWT 签名密钥首次启动随机生成并持久化
        if (!await settings.AnyAsync(s => s.Key == JwtSecretKey, ct))
        {
            var secret = new byte[64];
            System.Security.Cryptography.RandomNumberGenerator.Fill(secret);
            settings.Add(new AppSetting { Key = JwtSecretKey, Value = Convert.ToBase64String(secret) });
        }

        await db.SaveChangesAsync(ct);
    }
}
