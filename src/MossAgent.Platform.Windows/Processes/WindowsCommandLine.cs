using System.Text;

namespace MossAgent.Platform.Windows.Processes;

internal static class WindowsCommandLine
{
    public static StringBuilder Create(string executable, IReadOnlyList<string> arguments) =>
        new(string.Join(" ", new[] { executable }.Concat(arguments).Select(Quote)));

    private static string Quote(string argument)
    {
        ArgumentNullException.ThrowIfNull(argument);
        if (argument.Contains('\0')) throw new ArgumentException("Command arguments cannot contain null characters.");
        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in argument)
        {
            if (character == '\\') { backslashes++; continue; }
            result.Append('\\', character == '"' ? backslashes * 2 + 1 : backslashes);
            result.Append(character);
            backslashes = 0;
        }
        result.Append('\\', backslashes * 2);
        return result.Append('"').ToString();
    }
}
