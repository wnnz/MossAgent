using Avalonia.Controls.Documents;

namespace MossAgent.App.Controls;

internal sealed record MarkdownCodeRun(Run Inline, int Start);
