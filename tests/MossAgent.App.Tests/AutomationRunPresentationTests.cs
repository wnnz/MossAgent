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
    [InlineData(AutomationRunStatus.Blocked, false, false, true)]
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
    [InlineData("authorization-revision-changed", "RunBlockedReauthorization")]
    [InlineData("authorization-directories-reduced", "RunBlockedDirectoriesReduced")]
    [InlineData("provider-unavailable", "RunBlockedProviderMissing")]
    [InlineData("model-unavailable", "RunBlockedModelMissing")]
    [InlineData("project-unavailable", "RunBlockedProjectMissing")]
    [InlineData("snapshot-mismatch", "RunBlockedSnapshotMismatch")]
    [InlineData("snapshot-missing", "RunBlockedSnapshotMissing")]
    [InlineData("unexpected-code", "RunBlockedGeneric")]
    [InlineData(null, "RunBlockedGeneric")]
    public void BlockedRun_MapsReasonCodeToLocalizedHint(string? code, string key)
    {
        var run = new AutomationRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow,
            AutomationRunStatus.Blocked, false, DateTimeOffset.UtcNow, code);
        var model = new AutomationRunViewModel(run);
        Assert.Equal(AutomationText.Get("Blocked"), model.StatusLabel);
        Assert.Equal(AutomationText.Get(key), model.Detail);
        Assert.True(model.NeedsAttention);
        Assert.False(model.IsCompleted);
        Assert.False(model.IsFailure);
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
