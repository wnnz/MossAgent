using CodingAgent.Domain.Interfaces;
using System.Security.Cryptography;

namespace CodingAgent.Infrastructure.Security;

/// <summary>PBKDF2(SHA256, 100k 迭代) 密码哈希。</summary>
public class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 32;
    private const int HashSize = 32;
    private const int Iterations = 100_000;

    public string GenerateSalt()
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        return Convert.ToBase64String(salt);
    }

    public string Hash(string password, string salt)
    {
        var saltBytes = Convert.FromBase64String(salt);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, HashSize);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string password, string salt, string expectedHash)
    {
        try
        {
            var actual = Hash(password, salt);
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(actual),
                Convert.FromBase64String(expectedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
