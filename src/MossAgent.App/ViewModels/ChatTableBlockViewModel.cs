namespace MossAgent.App.ViewModels;

public sealed class ChatTableBlockViewModel
{
    public ChatTableBlockViewModel(
        IReadOnlyList<string> headers,
        IEnumerable<IReadOnlyList<string>> rows)
    {
        Headers = headers.ToArray();
        Rows = rows.Select(row => Enumerable.Range(0, Headers.Count)
            .Select(index => index < row.Count ? row[index] : string.Empty)
            .ToArray()).ToArray();
    }

    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<string[]> Rows { get; }
}
