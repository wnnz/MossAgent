using System.Collections.ObjectModel;

namespace MossAgent.App.ViewModels;

internal static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    public static void SynchronizeWith<T>(this ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        var comparer = EqualityComparer<T>.Default;
        var prefix = 0;
        while (prefix < target.Count && prefix < source.Count && comparer.Equals(target[prefix], source[prefix]))
            prefix++;
        if (prefix < target.Count && prefix < source.Count)
        {
            var retained = source.ToHashSet();
            for (var index = target.Count - 1; index >= prefix; index--)
            {
                if (!retained.Contains(target[index])) target.RemoveAt(index);
            }
        }
        for (var index = prefix; index < source.Count; index++)
        {
            var previous = index;
            while (previous < target.Count && !comparer.Equals(target[previous], source[index]))
                previous++;
            if (previous == target.Count) target.Insert(index, source[index]);
            else if (previous != index) target.Move(previous, index);
        }
        while (target.Count > source.Count) target.RemoveAt(target.Count - 1);
    }
}

