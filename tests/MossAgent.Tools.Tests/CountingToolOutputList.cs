using System.Collections;

namespace MossAgent.Tools.Tests;

internal sealed class CountingToolOutputList<TItem>(IReadOnlyList<TItem> items) : IReadOnlyList<TItem>
{
    public IReadOnlyList<TItem> Items => items;
    public int Count => items.Count;
    public int Reads { get; private set; }
    public TItem this[int index]
    {
        get
        {
            Reads++;
            return items[index];
        }
    }

    public IEnumerator<TItem> GetEnumerator()
    {
        for (var index = 0; index < Count; index++) yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
