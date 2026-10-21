using System.Collections;
using MossAgent.Tools.BuiltIn.Resources;

namespace MossAgent.Tools.BuiltIn.Execution;

internal sealed class CapturedToolRoots(IReadOnlyList<string> source) : IReadOnlyList<string>
{
    private readonly string[] _snapshot = source.ToArray();

    public int Count { get { Validate(); return _snapshot.Length; } }
    public string this[int index] { get { Validate(); return _snapshot[index]; } }

    public void Validate()
    {
        try
        {
            if (source.Count == _snapshot.Length
                && Enumerable.Range(0, _snapshot.Length).All(index => source[index] == _snapshot[index])) return;
        }
        catch (Exception exception) when (exception is ArgumentOutOfRangeException or IndexOutOfRangeException) { }
        throw new UnauthorizedAccessException(ToolExecutionText.Get("AuthorizedRootsChanged"));
    }

    public IEnumerator<string> GetEnumerator()
    {
        for (var index = 0; index < _snapshot.Length; index++)
        {
            Validate();
            yield return _snapshot[index];
        }
        Validate();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
