using System.Globalization;
using System.Text;
using MossAgent.App.ViewModels;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class ToolProgressTextOptimizationTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Names()
    {
        yield return [string.Empty];
        yield return [" \t\r\n\u0085\u2003 "];
        yield return ["file.read"];
        yield return ["\0file\r\n.read\t"];
        yield return [new string('a', 80)];
        yield return [new string('a', 81)];
        yield return [string.Concat(Enumerable.Repeat("😀", 81))];
        yield return [new string('a', 79) + "😀suffix"];
        yield return [new string('\0', 80) + "invisible"];
        yield return [new string('\0', 79) + "xignored"];
        yield return ["\ud800file\udc00"];
        yield return ["file\ud800\ud800\udc00\udc00"];
        yield return ["\u200b工具\u2028\u2029"];
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Summary_PreservesBoundedUnicodeAndLocalizedFormatting(string name)
    {
        foreach (var phase in Enum.GetValues<ToolExecutionPhase>())
            Assert.Equal(OriginalSummary(name, phase), WorkspaceToolProgressText.Summary(name, phase));
    }

    [Fact]
    public void Summary_MatchesOriginalAcrossDeterministicMixedUtf16Inputs()
    {
        var random = new Random(10612);
        string[] fragments = ["a", "工具", "😀", "\r\n", "\0", "\u0085", " ", "\ud800", "\udc00", "\u2003"];
        for (var sample = 0; sample < 500; sample++)
        {
            var name = new StringBuilder();
            var length = random.Next(200);
            for (var index = 0; index < length; index++) name.Append(fragments[random.Next(fragments.Length)]);
            var phase = (ToolExecutionPhase)(sample % 4);
            Assert.Equal(OriginalSummary(name.ToString(), phase), WorkspaceToolProgressText.Summary(name.ToString(), phase));
        }
    }

    [Fact]
    public void Summary_DoesNotAllocatePerRuneForOrdinaryToolNames()
    {
        const string name = "filesystem.read_file";
        const int repetitions = 1_000;
        for (var index = 0; index < 128; index++) WorkspaceToolProgressText.Summary(name, ToolExecutionPhase.Executing);
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < repetitions; index++) WorkspaceToolProgressText.Summary(name, ToolExecutionPhase.Executing);
        var bytes = (GC.GetAllocatedBytesForCurrentThread() - start) / repetitions;
        output.WriteLine($"Summary allocation: {bytes} bytes/operation");
        Assert.InRange(bytes, 0, 256);
    }

    private static string OriginalSummary(string name, ToolExecutionPhase phase)
    {
        var displayName = string.Concat(name.EnumerateRunes().Take(80).Where(rune => !Rune.IsControl(rune)));
        if (string.IsNullOrWhiteSpace(displayName)) displayName = WorkspaceText.Get("ToolProgressGenericName");
        return string.Format(CultureInfo.CurrentCulture, WorkspaceText.Get($"ToolPhase{phase}Summary"), displayName);
    }
}
