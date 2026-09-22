using System.Text;

namespace MossAgent.Tools.BuiltIn.Processes;

internal sealed class BoundedTextBuffer(int maximumCharacters)
{
    private readonly StringBuilder _builder = new();
    private readonly Lock _lock = new();
    private bool _truncated;

    public void Append(ReadOnlySpan<char> value)
    {
        lock (_lock)
        {
            var remaining = maximumCharacters - _builder.Length;
            if (remaining <= 0)
            {
                _truncated = true;
                return;
            }

            var length = Math.Min(value.Length, remaining);
            _builder.Append(value[..length]);
            _truncated |= length != value.Length;
        }
    }

    public void AppendLine(string? value)
    {
        if (value is null)
        {
            return;
        }

        Append(value);
        Append(Environment.NewLine);
    }

    public override string ToString()
    {
        lock (_lock)
        {
            return _truncated
                ? _builder + "\n[进程输出已截断]"
                : _builder.ToString();
        }
    }
}
