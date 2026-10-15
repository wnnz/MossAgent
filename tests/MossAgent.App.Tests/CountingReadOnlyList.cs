using System.Collections;

namespace MossAgent.App.Tests;

internal sealed class CountingReadOnlyList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
{
    public int Count => items.Count;
    public int ItemReads { get; private set; }
    public T this[int index]
    {
        get
        {
            ItemReads++;
            return items[index];
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
