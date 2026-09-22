namespace CodingAgent.Domain.Interfaces;

public interface IPasswordHasher
{
    string GenerateSalt();
    string Hash(string password, string salt);
    bool Verify(string password, string salt, string expectedHash);
}

public interface ITokenService
{
    string IssueToken(TimeSpan lifetime);
    bool TryValidate(string token);
}
