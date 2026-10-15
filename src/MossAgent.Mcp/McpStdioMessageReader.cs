using System.Text;

namespace MossAgent.Mcp;

internal sealed class McpStdioMessageReader(TextReader reader, int maximumLength = 2 * 1024 * 1024)
{
    private readonly char[] _buffer = new char[4096];
    private int _position;
    private int _count;

    public async Task<string?> ReadLineAsync(CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumLength);
        var line = new StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_position == _count)
            {
                _count = await reader.ReadAsync(_buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                _position = 0;
                if (_count == 0) return line.Length == 0 ? null : line.ToString();
            }

            var nextLine = Array.IndexOf(_buffer, '\n', _position, _count - _position);
            var end = nextLine < 0 ? _count : nextLine;
            var length = end - _position;
            if (line.Length + length > maximumLength)
                throw new IOException("MCP server 响应超过协议消息大小限制。");
            line.Append(_buffer, _position, length);
            _position = nextLine < 0 ? _count : nextLine + 1;
            if (nextLine < 0) continue;
            if (line.Length > 0 && line[^1] == '\r') line.Length--;
            return line.ToString();
        }
    }
}
