using System.Globalization;
using System.Text;

namespace MossAgent.Testing;

internal static class ProxyTestRequestReader
{
    public static async Task<(string Method, string Target, Dictionary<string, string> Headers, string Body)>
        ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        var single = new byte[1];
        while (bytes.Count < 16 * 1024)
        {
            await stream.ReadExactlyAsync(single, cancellationToken);
            bytes.Add(single[0]);
            if (bytes.Count >= 4 && bytes[^4] == '\r' && bytes[^3] == '\n'
                && bytes[^2] == '\r' && bytes[^1] == '\n')
                return await ReadBodyAsync(bytes, stream, cancellationToken);
        }
        throw new InvalidDataException("Proxy fixture header limit exceeded.");
    }

    private static async Task<(string Method, string Target, Dictionary<string, string> Headers, string Body)>
        ReadBodyAsync(List<byte> bytes, Stream stream, CancellationToken cancellationToken)
    {
        var lines = Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
        var start = lines[0].Split(' ');
        if (start.Length != 3) throw new InvalidDataException("Invalid proxy fixture request.");
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1).Where(line => line.Length > 0))
        {
            var separator = line.IndexOf(':');
            if (separator <= 0 || !headers.TryAdd(line[..separator], line[(separator + 1)..].Trim()))
                throw new InvalidDataException("Invalid proxy fixture headers.");
        }
        if (headers.ContainsKey("Transfer-Encoding"))
            throw new InvalidDataException("Proxy fixture requires a bounded content length.");
        var length = headers.TryGetValue("Content-Length", out var raw)
            ? int.Parse(raw, NumberStyles.None, CultureInfo.InvariantCulture) : 0;
        if (length < 0 || length > 1024 * 1024) throw new InvalidDataException("Proxy fixture body limit exceeded.");
        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return (start[0], start[1], headers, new UTF8Encoding(false, true).GetString(body));
    }
}
