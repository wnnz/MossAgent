using System.Text;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class Socks5HandshakeRejectionTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectionIsPublishedBeforeItsResponseCanBeObserved(bool missing)
    {
        var rejections = 0;
        var countAtResponse = -1;
        using var stream = new Socks5HandshakeTestStream(missing ? [5, 1, 0] : Authentication(false))
        {
            Writing = frame =>
            {
                if (IsRejection(frame)) countAtResponse = rejections;
            }
        };

        var accepted = await Socks5TestHandshake.AcceptAuthenticatedAsync(stream, "models.example.test", 80,
            "fixture-user", "fixture-password", TestContext.Current.CancellationToken, () => rejections++);

        Assert.False(accepted);
        Assert.Equal(1, rejections);
        Assert.Equal(1, countAtResponse);
        Assert.Equal(missing ? ["05FF"] : new[] { "0502", "0101" }, stream.Frames.Select(Convert.ToHexString));
    }

    [Fact]
    public async Task SuccessfulAuthenticationAndConnectNeverPublishRejection()
    {
        var host = Encoding.ASCII.GetBytes("models.example.test");
        var rejections = 0;
        using var stream = new Socks5HandshakeTestStream([.. Authentication(true), 5, 1, 0, 3, (byte)host.Length, .. host, 0, 80]);

        var accepted = await Socks5TestHandshake.AcceptAuthenticatedAsync(stream, "models.example.test", 80,
            "fixture-user", "fixture-password", TestContext.Current.CancellationToken, () => rejections++);

        Assert.True(accepted);
        Assert.Equal(0, rejections);
        Assert.Equal(new[] { "0502", "0100", "050000017F0000010000" }, stream.Frames.Select(Convert.ToHexString));
    }

    [Fact]
    public async Task MalformedGreetingDoesNotInventAnAuthenticationRejection()
    {
        var rejections = 0;
        using var stream = new Socks5HandshakeTestStream([4, 1, 2]);

        await Assert.ThrowsAsync<InvalidDataException>(() => Socks5TestHandshake.AcceptAuthenticatedAsync(stream,
            "models.example.test", 80, "fixture-user", "fixture-password", TestContext.Current.CancellationToken, () => rejections++));

        Assert.Equal(0, rejections);
        Assert.Empty(stream.Frames);
    }

    [Fact]
    public async Task FailedRejectionWriteStillPublishesTheDecisionExactlyOnce()
    {
        var rejections = 0;
        using var stream = new Socks5HandshakeTestStream(Authentication(false))
        {
            Writing = frame =>
            {
                if (IsRejection(frame)) throw new IOException("Synthetic rejected-response write failure.");
            }
        };

        await Assert.ThrowsAsync<IOException>(() => Socks5TestHandshake.AcceptAuthenticatedAsync(stream,
            "models.example.test", 80, "fixture-user", "fixture-password", TestContext.Current.CancellationToken, () => rejections++));

        Assert.Equal(1, rejections);
        Assert.Equal("0502", Convert.ToHexString(Assert.Single(stream.Frames)));
    }

    private static bool IsRejection(ReadOnlyMemory<byte> frame) =>
        frame.Span.SequenceEqual(new byte[] { 5, 255 }) || frame.Span.SequenceEqual(new byte[] { 1, 1 });

    private static byte[] Authentication(bool accepted)
    {
        var username = Encoding.UTF8.GetBytes("fixture-user");
        var password = Encoding.UTF8.GetBytes(accepted ? "fixture-password" : "wrong-password");
        return [5, 1, 2, 1, (byte)username.Length, .. username, (byte)password.Length, .. password];
    }
}
