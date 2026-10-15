using System.Text;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class TextReplacementEncodingTests
{
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf8-bom")]
    [InlineData("utf16-le")]
    [InlineData("utf16-be")]
    [InlineData("utf32-le")]
    [InlineData("utf32-be")]
    public async Task AtomicReplacementPreservesEncodingPreambleAndNewlines(string format)
    {
        using var fixture = new FileEditingFixture();
        Encoding encoding = format switch
        {
            "utf8" => new UTF8Encoding(false),
            "utf8-bom" => new UTF8Encoding(true),
            "utf16-le" => new UnicodeEncoding(false, true),
            "utf16-be" => new UnicodeEncoding(true, true),
            "utf32-le" => new UTF32Encoding(false, true),
            "utf32-be" => new UTF32Encoding(true, true),
            _ => throw new InvalidOperationException()
        };
        await fixture.SeedAsync("alpha\r\n中文 beta\n", encoding);
        var version = await fixture.SnapshotAsync();

        var result = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "beta", newText = "gamma", expectedSha256 = version.Sha256 });

        Assert.True(result.IsSuccess, result.Summary);
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes("alpha\r\n中文 gamma\n")).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }

    [Fact]
    public async Task InvalidUtf8IsNotSilentlyReencodedByReplacement()
    {
        using var fixture = new FileEditingFixture();
        byte[] bytes = [0x61, 0x62, 0x63, 0xFF];
        await File.WriteAllBytesAsync(fixture.Path, bytes, fixture.Token);

        var result = await fixture.ExecuteAsync(fixture.Replace,
            new { path = fixture.Path, oldText = "abc", newText = "changed" });

        Assert.False(result.IsSuccess);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(fixture.Path, fixture.Token));
        fixture.AssertNoTemporaryFiles();
    }
}
