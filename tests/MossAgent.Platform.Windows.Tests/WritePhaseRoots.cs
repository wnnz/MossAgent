using System.Collections;

namespace MossAgent.Platform.Windows.Tests;

internal sealed class WritePhaseRoots(string root, Action action) : IReadOnlyList<string>
{
    public int Count => 1;
    public string this[int index] => index == 0 ? root : throw new IndexOutOfRangeException();

    public IEnumerator<string> GetEnumerator()
    {
        action();
        yield return root;
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
