using System.Buffers.Binary;
using System.Net;
using System.Text;

namespace MossAgent.Testing;

internal static class Socks5TestHandshake
{
    public static async Task AcceptAsync(Stream stream, string expectedHost, int expectedPort,
        CancellationToken cancellationToken)
    {
        var greeting = await ReadAsync(stream, 2, cancellationToken);
        if (greeting[0] != 5 || greeting[1] == 0) throw new InvalidDataException("Invalid SOCKS5 greeting.");
        var methods = await ReadAsync(stream, greeting[1], cancellationToken);
        if (!methods.Contains((byte)0)) throw new InvalidDataException("Anonymous SOCKS5 method missing.");
        await stream.WriteAsync(new byte[] { 5, 0 }, cancellationToken);

        await AcceptConnectAsync(stream, expectedHost, expectedPort, cancellationToken);
    }

    public static async Task<bool> AcceptAuthenticatedAsync(Stream stream, string expectedHost, int expectedPort,
        string username, string password, CancellationToken cancellationToken, Action? onRejected = null)
    {
        var greeting = await ReadAsync(stream, 2, cancellationToken);
        if (greeting[0] != 5 || greeting[1] == 0) throw new InvalidDataException("Invalid SOCKS5 greeting.");
        var methods = await ReadAsync(stream, greeting[1], cancellationToken);
        if (!methods.Contains((byte)2))
        {
            onRejected?.Invoke();
            await stream.WriteAsync(new byte[] { 5, 255 }, cancellationToken);
            return false;
        }
        await stream.WriteAsync(new byte[] { 5, 2 }, cancellationToken);
        var authentication = await ReadAsync(stream, 2, cancellationToken);
        if (authentication[0] != 1 || authentication[1] == 0)
            throw new InvalidDataException("Invalid SOCKS5 authentication.");
        var suppliedUsername = await ReadAsync(stream, authentication[1], cancellationToken);
        var passwordLength = (await ReadAsync(stream, 1, cancellationToken))[0];
        var suppliedPassword = await ReadAsync(stream, passwordLength, cancellationToken);
        var accepted = suppliedUsername.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(username))
            && suppliedPassword.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(password));
        if (!accepted) onRejected?.Invoke();
        await stream.WriteAsync(new byte[] { 1, accepted ? (byte)0 : (byte)1 }, cancellationToken);
        if (accepted) await AcceptConnectAsync(stream, expectedHost, expectedPort, cancellationToken);
        return accepted;
    }

    private static async Task AcceptConnectAsync(Stream stream, string expectedHost, int expectedPort,
        CancellationToken cancellationToken)
    {

        var request = await ReadAsync(stream, 4, cancellationToken);
        if (request[0] != 5 || request[1] != 1 || request[2] != 0)
            throw new InvalidDataException("Invalid SOCKS5 CONNECT request.");
        var host = await ReadHostAsync(stream, request[3], cancellationToken);
        var port = BinaryPrimitives.ReadUInt16BigEndian(await ReadAsync(stream, 2, cancellationToken));
        if (!StringComparer.OrdinalIgnoreCase.Equals(host, expectedHost) || port != expectedPort)
            throw new InvalidDataException("Unexpected SOCKS5 target.");
        await stream.WriteAsync(new byte[] { 5, 0, 0, 1, 127, 0, 0, 1, 0, 0 }, cancellationToken);
    }

    private static async Task<string> ReadHostAsync(Stream stream, byte addressType, CancellationToken cancellationToken)
    {
        if (addressType == 3)
        {
            var length = (await ReadAsync(stream, 1, cancellationToken))[0];
            if (length == 0) throw new InvalidDataException("Empty SOCKS5 target.");
            return Encoding.ASCII.GetString(await ReadAsync(stream, length, cancellationToken));
        }
        var count = addressType switch
        {
            1 => 4,
            4 => 16,
            _ => throw new InvalidDataException("Unsupported SOCKS5 address type.")
        };
        return new IPAddress(await ReadAsync(stream, count, cancellationToken)).ToString();
    }

    private static async Task<byte[]> ReadAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, cancellationToken);
        return bytes;
    }
}
