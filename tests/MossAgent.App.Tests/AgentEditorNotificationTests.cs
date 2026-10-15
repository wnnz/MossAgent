using Xunit;

namespace MossAgent.App.Tests;

public sealed class AgentEditorNotificationTests
{
    [Fact]
    public async Task FailedWorkspaceNotification_DistinguishesTheCommittedSaveAndRefreshRetriesWithoutWriting()
    {
        using var fixture = new AgentEditorTestFixture();
        var settings = fixture.Settings;
        var notifications = 0;
        var fail = true;
        settings.ProfilesChanged += () =>
        {
            notifications++;
            return fail ? Task.FromException(new InvalidOperationException("fixture-secret-error")) : Task.CompletedTask;
        };

        await settings.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(fixture.Repository.Profiles);
        Assert.Equal(saved.Id, settings.SelectedProfile?.Id);
        Assert.True(settings.HasStatusError);
        Assert.Contains("已写入", settings.Status);
        Assert.DoesNotContain("fixture-secret-error", settings.Status);
        fail = false;
        settings.SystemPrompt = "newer draft";
        await settings.RefreshCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError);
        Assert.Equal(2, notifications);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.Equal("newer draft", settings.SystemPrompt);
    }

    [Fact]
    public async Task NoncooperativeRefreshNotification_IsBoundedAndBlocksOtherWrites()
    {
        using var fixture = new AgentEditorTestFixture(TimeSpan.FromMilliseconds(80));
        var settings = fixture.Settings;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        settings.ProfilesChanged += () => gate.Task;
        var refreshing = settings.RefreshCommand.ExecuteAsync(null);
        Assert.True(settings.IsLoading);
        Assert.False(settings.SaveCommand.CanExecute(null));
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Equal(0, fixture.Repository.SaveCalls);
        await refreshing.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(settings.IsLoading);
        Assert.Contains("超时", settings.Status);
        Assert.Equal("original prompt", settings.SystemPrompt);
        gate.SetResult();
    }

    [Fact]
    public async Task FailedPostSaveRead_DoesNotMisreportTheCommittedTargetAsDeleted()
    {
        using var fixture = new AgentEditorTestFixture();
        fixture.Repository.BeforeSave = _ =>
        {
            fixture.Repository.Loader = _ => Task.FromException<IReadOnlyList<MossAgent.Domain.AgentProfile>>(
                new InvalidOperationException("fixture-secret-error"));
            return Task.CompletedTask;
        };

        await fixture.Settings.SaveCommand.ExecuteAsync(null);

        var saved = Assert.Single(fixture.Repository.Profiles);
        Assert.Equal(saved.Id, fixture.Settings.SelectedProfile?.Id);
        Assert.True(fixture.Settings.HasStatusError);
        Assert.Contains("加载", fixture.Settings.Status);
        Assert.DoesNotContain("fixture-secret-error", fixture.Settings.Status);
        Assert.False(fixture.Settings.IsEditorTargetUnavailable);
        Assert.False(fixture.Settings.SaveCommand.CanExecute(null));
        fixture.Repository.Loader = null;
        await fixture.Settings.RefreshCommand.ExecuteAsync(null);
        Assert.False(fixture.Settings.HasStatusError);
        Assert.Equal(saved.Id, Assert.Single(fixture.Settings.Items).Id);
        Assert.Equal(1, fixture.Repository.SaveCalls);
        Assert.True(fixture.Settings.SaveCommand.CanExecute(null));
    }

    [Fact]
    public async Task FailedSave_PreservesTheDraftAndUsesASafeActionableError()
    {
        using var fixture = new AgentEditorTestFixture();
        fixture.Repository.BeforeSave = _ => Task.FromException(new InvalidOperationException("fixture-secret-error"));

        await fixture.Settings.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Repository.Profiles);
        Assert.Equal("original", fixture.Settings.Name);
        Assert.Equal("original prompt", fixture.Settings.SystemPrompt);
        Assert.True(fixture.Settings.HasStatusError);
        Assert.Contains("草稿已保留", fixture.Settings.Status);
        Assert.DoesNotContain("fixture-secret-error", fixture.Settings.Status);
        fixture.Repository.BeforeSave = null;
        await fixture.Settings.SaveCommand.ExecuteAsync(null);
        Assert.Single(fixture.Repository.Profiles);
        Assert.False(fixture.Settings.HasStatusError);
    }
}
