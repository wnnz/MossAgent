using System.Net;
using System.Text;

namespace MossAgent.Providers.Tests;

public sealed class UnknownLengthResponseContent(string body) : HttpContent
{
    private readonly byte[] _bytes = Encoding.UTF8.GetBytes(body);

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        await stream.WriteAsync(_bytes);
    }

    protected override Task<Stream> CreateContentReadStreamAsync() =>
        Task.FromResult<Stream>(new MemoryStream(_bytes, writable: false));

    protected override bool TryComputeLength(out long length)
    {
        length = 0;
        return false;
    }
}
