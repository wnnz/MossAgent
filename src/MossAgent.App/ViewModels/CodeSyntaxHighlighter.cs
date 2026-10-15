using System.Text.RegularExpressions;

namespace MossAgent.App.ViewModels;

public static partial class CodeSyntaxHighlighter
{
    public static IReadOnlyList<CodeSyntaxToken> Tokenize(string language, string code)
    {
        if (code.Length == 0) return [];
        if (!Supports(language)) return [new CodeSyntaxToken(code, string.Empty)];

        var tokens = new List<CodeSyntaxToken>();
        var cursor = 0;
        foreach (Match match in TokenPattern().Matches(code))
        {
            if (match.Index > cursor)
            {
                tokens.Add(new CodeSyntaxToken(code[cursor..match.Index], string.Empty));
            }

            tokens.Add(new CodeSyntaxToken(match.Value, GetResourceKey(match)));
            cursor = match.Index + match.Length;
        }

        if (cursor < code.Length) tokens.Add(new CodeSyntaxToken(code[cursor..], string.Empty));
        return tokens;
    }

    private static bool Supports(string language) => language.Trim().ToLowerInvariant() is
        "csharp" or "cs" or "c#" or "javascript" or "js" or "typescript" or "ts"
        or "python" or "py" or "json" or "bash" or "shell" or "sh" or "sql";

    private static string GetResourceKey(Match match)
    {
        if (match.Groups["comment"].Success) return "CodeCommentBrush";
        if (match.Groups["string"].Success) return "CodeStringBrush";
        if (match.Groups["number"].Success) return "CodeNumberBrush";
        if (match.Groups["keyword"].Success) return "CodeKeywordBrush";
        if (match.Groups["type"].Success) return "CodeTypeBrush";
        return string.Empty;
    }

    [GeneratedRegex(
        """(?<comment>//[^\r\n]*|/\*[\s\S]*?\*/|#[^\r\n]*)|(?<string>"(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*'|`(?:\\.|[^`\\])*`)|(?<number>\b\d+(?:\.\d+)?(?:[eE][+-]?\d+)?\b)|(?<keyword>\b(?:abstract|as|async|await|base|bool|break|byte|case|catch|char|class|const|continue|def|delegate|do|double|else|enum|export|false|finally|float|for|foreach|function|if|import|in|int|interface|internal|is|let|long|namespace|new|null|object|package|private|protected|public|return|self|static|string|struct|switch|this|throw|true|try|using|var|void|while|with|yield)\b)|(?<type>\b[A-Z][A-Za-z0-9_]*\b)""",
        RegexOptions.Compiled)]
    private static partial Regex TokenPattern();
}
