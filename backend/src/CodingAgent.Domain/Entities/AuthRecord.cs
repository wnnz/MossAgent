namespace CodingAgent.Domain.Entities;

/// <summary>认证记录（单行）：PBKDF2 密码哈希。</summary>
public class AuthRecord
{
    public int Id { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public string Salt { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
