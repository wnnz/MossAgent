using System.Collections;
using MossAgent.Application.Models;

namespace MossAgent.Agent.Tests;

internal sealed class ModelMessageAccessProbe(IReadOnlyList<ModelMessage> messages) : IReadOnlyList<ModelMessage>
{
    public int Count => messages.Count;
    public int Reads { get; private set; }

    public ModelMessage this[int index]
    {
        get
        {
            Reads++;
            return messages[index];
        }
    }

    public IEnumerator<ModelMessage> GetEnumerator()
    {
        for (var index = 0; index < Count; index++)
            yield return this[index];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
