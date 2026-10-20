using MossAgent.Platform.Windows.Tests;
using Xunit;

namespace MossAgent.Platform.Windows.Tests;

public sealed class PtyPidPublicationTests
{
    [Fact]
    public async Task BindingWaitsUntilThePidPublisherHasClosedItsFile()
    {
        await using var fixture = new PtyNativeFixture();
        var token = TestContext.Current.CancellationToken;
        var task = Guid.NewGuid();
        await fixture.StartAsync(task);
        await fixture.WriteAsync(task, PtyNativeFixture.PublishPid("identity.pid") + "; [IO.File]::WriteAllText('setup.ready', '')");
        await fixture.WaitForFileAsync("setup.ready");
        var pid = await File.ReadAllTextAsync(Path.Combine(fixture.Root, "identity.pid"), token);
        Task<System.Diagnostics.Process> binding;
        await using (var writer = new FileStream(Path.Combine(fixture.Root, "held.pid"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            await writer.WriteAsync(System.Text.Encoding.UTF8.GetBytes(pid), token);
            await writer.FlushAsync(token);
            binding = fixture.BindAsync("held.pid");
            var prematureFailure = binding.IsCompleted ? await Record.ExceptionAsync(() => binding) : null;
            Assert.Null(prematureFailure);
            Assert.False(binding.IsCompleted);
        }
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "held.pid.ready"), string.Empty, token);
        var process = await binding.WaitAsync(PtyNativeFixture.Deadline, token);
        Assert.Equal(int.Parse(pid), process.Id);
        Assert.False(process.HasExited);
        await fixture.StopAsync(task);
        await PtyNativeFixture.AssertExitedAsync(process);
    }
}
