using System.Text;
using System.Text.Json;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class DiffInputEncodingTests
{
    [Theory]
    [InlineData("utf8", false)]
    [InlineData("utf16", false)]
    [InlineData("utf16be", false)]
    [InlineData("utf32", false)]
    [InlineData("utf32be", false)]
    [InlineData("utf8", true)]
    [InlineData("utf16", true)]
    [InlineData("utf16be", true)]
    [InlineData("utf32", true)]
    [InlineData("utf32be", true)]
    public async Task NativeInputsKeepUnicodeCrLfAndMissingNewlineDiffSemantics(string name, bool statistics)
    {
        Encoding encoding = name switch
        {
            "utf8" => new UTF8Encoding(true, true),
            "utf16" => Encoding.Unicode,
            "utf16be" => Encoding.BigEndianUnicode,
            "utf32" => Encoding.UTF32,
            _ => new UTF32Encoding(true, true, true)
        };
        using var fixture = await DiffInputFlowFixture.CreateAsync(encoding, "原始\r\n保留\r\n", "更新\r\n保留");
        var result = await fixture.ExecuteAsync(statistics);
        Assert.True(result.IsSuccess, result.Summary);
        if (statistics) Assert.Equal(new ToolFileChangeStatistics(2, 2), JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!));
        else
        {
            var patch = await File.ReadAllTextAsync(Assert.Single(result.Artifacts!).Path, fixture.Files.Token);
            Assert.Contains("-原始", patch);
            Assert.Contains("+更新", patch);
            Assert.Contains("No newline at end of file", patch);
        }
        Assert.All(fixture.Inputs.Sessions, session => Assert.True(session.IsDisposed));
        Assert.Empty(Directory.GetFiles(fixture.Files.ArtifactRoot, "diff-input-*"));
    }

    [Fact]
    public async Task MaximumSnapshotUtf16ExpansionStillFitsInputProtocolAndProducesNumstat()
    {
        using var fixture = await DiffInputFlowFixture.CreateAsync(Encoding.Unicode, new string('中', 1_000_000), "更新");
        var result = await fixture.ExecuteAsync(true);
        Assert.True(result.IsSuccess, result.Summary);
        Assert.Equal(new ToolFileChangeStatistics(1, 1), JsonSerializer.Deserialize<ToolFileChangeStatistics>(result.Content!));
        Assert.All(fixture.Inputs.Sessions, session => Assert.True(session.IsDisposed));
    }
}
