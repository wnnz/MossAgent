using System.Text;
using System.Text.RegularExpressions;

namespace MossAgent.App.ViewModels;

public static partial class ChatContentParser
{
    public static IReadOnlyList<object> Parse(string content)
    {
        if (string.IsNullOrEmpty(content)) return [];
        var blocks = new List<object>();
        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var language = string.Empty;
        var inCode = false;
        var lines = content.Replace("\r\n", "\n").Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            ProcessLine(lines, ref lineIndex, blocks, paragraph, code, ref language, ref inCode);
        }
        FlushParagraph(blocks, paragraph);
        if (inCode)
            blocks.Add(new ChatCodeBlockViewModel(language, code.ToString().TrimEnd('\r', '\n')));
        return blocks;
    }

    private static void ProcessLine(
        string[] lines,
        ref int lineIndex,
        List<object> blocks,
        StringBuilder paragraph,
        StringBuilder code,
        ref string language,
        ref bool inCode)
    {
        var line = lines[lineIndex];
        if (TryHandleCodeFence(line, blocks, paragraph, code, ref language, ref inCode)) return;
        if (inCode)
        {
            code.AppendLine(line);
            return;
        }
        if (string.IsNullOrWhiteSpace(line))
        {
            FlushParagraph(blocks, paragraph);
            return;
        }
        if (TryAddTable(blocks, paragraph, lines, ref lineIndex)) return;
        if (TryAddSpecialLine(blocks, paragraph, line)) return;
        if (paragraph.Length > 0) paragraph.AppendLine();
        paragraph.Append(line);
    }

    private static bool TryHandleCodeFence(
        string line,
        List<object> blocks,
        StringBuilder paragraph,
        StringBuilder code,
        ref string language,
        ref bool inCode)
    {
        if (!line.StartsWith("```", StringComparison.Ordinal)) return false;
        FlushParagraph(blocks, paragraph);
        if (inCode)
        {
            blocks.Add(new ChatCodeBlockViewModel(
                language, code.ToString().TrimEnd('\r', '\n')));
            code.Clear();
        }
        else
        {
            language = line[3..].Trim();
        }
        inCode = !inCode;
        return true;
    }

    private static bool TryAddTable(
        List<object> blocks,
        StringBuilder paragraph,
        string[] lines,
        ref int lineIndex)
    {
        if (lineIndex + 1 >= lines.Length
            || (!lines[lineIndex].Contains('|') && !lines[lineIndex + 1].Contains('|')))
        {
            return false;
        }

        var separators = SplitTableRow(lines[lineIndex + 1]);
        if (!separators.All(IsTableSeparatorCell))
        {
            return false;
        }
        var headers = SplitTableRow(lines[lineIndex]);
        if (separators.Length != headers.Length) return false;

        FlushParagraph(blocks, paragraph);
        var rows = new List<IReadOnlyList<string>>();
        var tableEndIndex = lineIndex + 1;
        for (var rowIndex = lineIndex + 2; rowIndex < lines.Length; rowIndex++)
        {
            if (string.IsNullOrWhiteSpace(lines[rowIndex]) || !lines[rowIndex].Contains('|'))
            {
                break;
            }

            rows.Add(SplitTableRow(lines[rowIndex]));
            tableEndIndex = rowIndex;
        }

        blocks.Add(new ChatTableBlockViewModel(headers, rows));
        lineIndex = tableEndIndex;
        return true;
    }

    private static string[] SplitTableRow(string line)
    {
        var source = line.Trim();
        if (source.StartsWith('|')) source = source[1..];
        if (source.EndsWith('|') && !source.EndsWith("\\|")) source = source[..^1];

        var cells = new List<string>();
        var cell = new StringBuilder();
        var insideCode = false;
        for (var index = 0; index < source.Length; index++)
        {
            var character = source[index];
            if (character == '\\' && index + 1 < source.Length && source[index + 1] == '|')
            {
                cell.Append('|');
                index++;
                continue;
            }

            if (character == '`') insideCode = !insideCode;
            if (character == '|' && !insideCode)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
                continue;
            }

            cell.Append(character);
        }

        cells.Add(cell.ToString().Trim());
        return cells.ToArray();
    }

    private static bool IsTableSeparatorCell(string cell)
    {
        var value = cell.Trim();
        return value.Count(static character => character == '-') >= 3
            && value.All(static character => character is '-' or ':');
    }

    private static bool TryAddSpecialLine(
        List<object> blocks,
        StringBuilder paragraph,
        string line)
    {
        var heading = HeadingPattern().Match(line);
        if (heading.Success)
        {
            FlushParagraph(blocks, paragraph);
            blocks.Add(new ChatTextBlockViewModel(heading.Groups[2].Value, isHeading: true));
            return true;
        }
        var list = ListPattern().Match(line);
        if (list.Success)
        {
            FlushParagraph(blocks, paragraph);
            blocks.Add(new ChatTextBlockViewModel(
                list.Groups[2].Value,
                isListItem: true,
                prefix: NormalizePrefix(list.Groups[1].Value),
                indentLevel: GetListIndentLevel(line)));
            return true;
        }
        if (line.StartsWith("> ", StringComparison.Ordinal))
        {
            FlushParagraph(blocks, paragraph);
            blocks.Add(new ChatTextBlockViewModel(line[2..], isQuote: true));
            return true;
        }
        return false;
    }

    private static string NormalizePrefix(string prefix) =>
        char.IsDigit(prefix[0]) ? prefix : "•";

    private static int GetListIndentLevel(string line) =>
        line.TakeWhile(char.IsWhiteSpace).Sum(static character => character == '\t' ? 4 : 1) / 2;

    private static void FlushParagraph(List<object> blocks, StringBuilder paragraph)
    {
        if (paragraph.Length == 0) return;
        blocks.Add(new ChatTextBlockViewModel(paragraph.ToString()));
        paragraph.Clear();
    }

    [GeneratedRegex("^(#{1,6})\\s+(.+)$")]
    private static partial Regex HeadingPattern();

    [GeneratedRegex("^\\s*((?:[-+*])|(?:\\d+\\.))\\s+(.+)$")]
    private static partial Regex ListPattern();
}
