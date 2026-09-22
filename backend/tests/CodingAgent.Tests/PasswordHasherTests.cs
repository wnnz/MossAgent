using CodingAgent.Infrastructure.Security;
using Xunit;

namespace CodingAgent.Tests;

public class PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void GenerateSalt_ReturnsUniqueBase64()
    {
        var salt1 = _hasher.GenerateSalt();
        var salt2 = _hasher.GenerateSalt();
        Assert.NotEqual(salt1, salt2);
        // 有效 base64 且 32 字节
        Assert.Equal(32, Convert.FromBase64String(salt1).Length);
    }

    [Fact]
    public void Hash_Verify_CorrectPassword()
    {
        var salt = _hasher.GenerateSalt();
        var hash = _hasher.Hash("s3cret-密码", salt);
        Assert.True(_hasher.Verify("s3cret-密码", salt, hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var salt = _hasher.GenerateSalt();
        var hash = _hasher.Hash("correct", salt);
        Assert.False(_hasher.Verify("wrong", salt, hash));
    }

    [Fact]
    public void Verify_InvalidHash_ReturnsFalse()
    {
        var salt = _hasher.GenerateSalt();
        Assert.False(_hasher.Verify("x", salt, "not-base64!!!"));
    }

    [Fact]
    public void Hash_SamePasswordDifferentSalt_DifferentHash()
    {
        var salt1 = _hasher.GenerateSalt();
        var salt2 = _hasher.GenerateSalt();
        Assert.NotEqual(_hasher.Hash("pw", salt1), _hasher.Hash("pw", salt2));
    }
}
