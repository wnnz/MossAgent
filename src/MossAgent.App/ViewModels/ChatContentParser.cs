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
        foreach (var line in content.Replace("\r\n", "\n").Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
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
                continue;
            }
            if (inCode)
            {
                code.AppendLine(line);
                continue;
            }
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(blocks, paragraph);
                continue;
            }
            if (TryAddSpecialLine(blocks, paragraph, line))
            {
                continue;
            }
            if (paragraph.Length > 0) paragraph.AppendLine();
            paragraph.Append(line);
        }
        FlushParagraph(blocks, paragraph);
        if (inCode)
            blocks.Add(new ChatCodeBlockViewModel(language, code.ToString().TrimEnd('\r', '\n')));
        return blocks;
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
                list.Groups[2].Value, isListItem: true, prefix: NormalizePrefix(list.Groups[1].Value)));
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
