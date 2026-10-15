namespace MossAgent.App.Terminal;

internal sealed class TerminalControlStringBoundary
{
    private bool _escape;
    private bool _string;
    private bool _osc;

    public int Append(char character, Span<char> destination)
    {
        if (_string)
        {
            if (character is '\u0018' or '\u001a' or '\u009c' || _osc && character == '\a')
            { _string = false; _escape = false; return 0; }
            if (!_escape) { _escape = character == '\u001b'; return 0; }
            if (character == '\\') { _string = false; _escape = false; return 0; }
            if (character == '\u001b') return 0;
            _string = false;
        }
        if (_escape)
        {
            _escape = false;
            if (character is ']' or 'P' or 'X' or '^' or '_')
            { _string = true; _osc = character == ']'; return 0; }
            destination[0] = '\u001b';
            destination[1] = character;
            return 2;
        }
        if (character == '\u001b') { _escape = true; return 0; }
        if (character is '\u009d' or '\u0090' or '\u0098' or '\u009e' or '\u009f')
        { _string = true; _osc = character == '\u009d'; return 0; }
        if (character == '\u009c') return 0;
        if (character == '\u009b') { destination[0] = '\u001b'; destination[1] = '['; return 2; }
        destination[0] = character;
        return 1;
    }
}
