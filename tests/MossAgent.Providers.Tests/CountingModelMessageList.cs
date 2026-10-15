using System.Collections;
using MossAgent.Application.Models;

namespace MossAgent.Providers.Tests;

internal sealed class CountingModelMessageList(IReadOnlyList<ModelMessage> messages) : IReadOnlyList<ModelMessage>
{
    public int EnumerationCount { get; private set; }
    public int Count => messages.Count;
    public ModelMessage this[int index] => messages[index];

    public IEnumerator<ModelMessage> GetEnumerator()
    {
        EnumerationCount++;
        return messages.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
