using System.Text.Json;

namespace MossAgent.Tools.BuiltIn.Git;

public static class GitHunkOrdinals
{
    public const int MaximumCount = 64;
    public const int MaximumValue = 999;

    public static int[] Read(JsonElement arguments)
    {
        if (!arguments.TryGetProperty("hunks", out var element)
            || element.ValueKind != JsonValueKind.Array
            || element.GetArrayLength() is 0 or > MaximumCount)
        {
            throw new ArgumentException();
        }

        var ordinals = new int[element.GetArrayLength()];
        var index = 0;
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Number
                || !item.TryGetInt32(out var ordinal)
                || ordinal < 0 || ordinal > MaximumValue)
            {
                throw new ArgumentException();
            }
            ordinals[index++] = ordinal;
        }
        return ordinals;
    }
}
