using System.Text;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyFilePublicationTests
{
    [Fact]
    public async Task PayloadReadWaitsUntilTheWriterPublishesCompletion()
    {
        await using var fixture = new PtyNativeFixture();
        var token = TestContext.Current.CancellationToken;
        Task<string> reading;
        await using (var writer = new FileStream(Path.Combine(fixture.Root, "payload.txt"),
            FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await writer.WriteAsync(Encoding.UTF8.GetBytes("中文🚀"), token);
            await writer.FlushAsync(token);
            reading = fixture.ReadPublishedFileAsync("payload.txt");
            var prematureFailure = reading.IsCompleted ? await Record.ExceptionAsync(() => reading) : null;
            Assert.Null(prematureFailure);
            Assert.False(reading.IsCompleted);
        }
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "payload.txt.ready"), string.Empty, token);
        Assert.Equal("中文🚀", await reading.WaitAsync(PtyNativeFixture.Deadline, token));
    }
}
