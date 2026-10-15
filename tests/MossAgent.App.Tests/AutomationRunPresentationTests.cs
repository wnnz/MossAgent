using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AutomationRunPresentationTests
{
    [Theory]
    [InlineData(AutomationRunStatus.Running, false, false, false)]
    [InlineData(AutomationRunStatus.Completed, true, false, false)]
    [InlineData(AutomationRunStatus.Failed, false, true, false)]
    [InlineData(AutomationRunStatus.TimedOut, false, true, false)]
    [InlineData(AutomationRunStatus.Cancelled, false, false, true)]
    [InlineData(AutomationRunStatus.Interrupted, false, false, true)]
    public void StatusDetails_AreLocalizedAndNeverEchoUntrustedErrorCodes(AutomationRunStatus status, bool completed, bool failure, bool attention)
    {
        var start = new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero);
        var run = new AutomationRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, status, true,
            start.AddSeconds(42.25), "api-key=must-never-display");
        var model = new AutomationRunViewModel(run);
        Assert.Equal(completed, model.IsCompleted);
        Assert.Equal(failure, model.IsFailure);
        Assert.Equal(attention, model.NeedsAttention);
        Assert.Equal(failure || attention, model.HasDetail);
        Assert.DoesNotContain("must-never-display", model.Detail);
        Assert.Equal(AutomationText.Format("RunDuration", 43), model.DurationLabel);
        Assert.Equal(AutomationText.Get(status.ToString()), model.StatusLabel);
    }

    [Fact]
    public void KnownFailure_UsesActionableHintWithoutExposingTheCode()
    {
        var run = new AutomationRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            AutomationRunStatus.Failed, false, ErrorCode: "execution-unavailable");
        Assert.Equal(AutomationText.Get("ExecutionUnavailable"), new AutomationRunViewModel(run).Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(-1)]
    public void UnknownOrInvalidDuration_DoesNotInventElapsedTime(int? seconds)
    {
        var start = DateTimeOffset.UtcNow;
        var run = new AutomationRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), start, AutomationRunStatus.Running,
            false, seconds is null ? null : start.AddSeconds(seconds.Value));
        Assert.Equal(AutomationText.Get("RunNotFinished"), new AutomationRunViewModel(run).DurationLabel);
    }
}
