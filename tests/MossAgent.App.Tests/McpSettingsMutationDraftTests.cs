using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class McpSettingsMutationDraftTests
{
    [Fact]
    public async Task Save_LateReadDoesNotReplaceNewerInputWithSavedSnapshot()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        fixture.Model.Name = "captured-name";
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = fixture.Model.SaveCommand.ExecuteAsync(null);
        fixture.Model.Name = "newer-unsaved-name";
        fixture.Model.Command = "newer-unsaved-command";
        fixture.Repository.Reader = null;

        pending.SetResult([original]);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("captured-name", Assert.Single(fixture.Repository.Items).Name);
        Assert.Equal("newer-unsaved-name", fixture.Model.Name);
        Assert.Equal("newer-unsaved-command", fixture.Model.Command);
        Assert.True(fixture.Model.IsFormOpen);
        Assert.Equal(original.Id, fixture.Model.SelectedItem?.Id);
        Assert.DoesNotContain("已保存 MCP server", fixture.Model.Status);
    }

    [Fact]
    public async Task Save_ClosedEditorCannotBeReopenedByLateConnectionFailure()
    {
        using var fixture = new McpSettingsReloadFixture(blockReconnect: true);
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        fixture.Manager.Errors = new Dictionary<string, string> { [original.Name] = "safe-connection-failure" };
        var run = fixture.Model.SaveCommand.ExecuteAsync(null);
        await fixture.Manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        fixture.Model.CloseFormCommand.Execute(null);

        fixture.Manager.Release.TrySetResult();
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.False(fixture.Model.IsFormOpen);
        Assert.Equal(1, fixture.Repository.WriteCalls);
        Assert.Equal(original.Id, Assert.Single(fixture.Repository.Items).Id);
    }

    [Fact]
    public async Task Save_RemovedTargetIsNotSilentlyRecreated()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        fixture.Model.Name = "draft-for-removed-server";
        fixture.Repository.Items = [];

        await fixture.Model.SaveCommand.ExecuteAsync(null);

        Assert.Empty(fixture.Repository.Items);
        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.True(fixture.Model.IsFormOpen);
        Assert.Equal("draft-for-removed-server", fixture.Model.Name);
        Assert.Contains("不存在", fixture.Model.Status);
    }

    [Fact]
    public async Task Save_NewDraftKeepsItsPersistedIdentityWhenInputChangesDuringSave()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Model.OpenCreateFormCommand.Execute(null);
        fixture.Model.Name = "captured-new-name";
        fixture.Model.Command = "not-started";
        fixture.Model.IsEnabled = false;
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = fixture.Model.SaveCommand.ExecuteAsync(null);
        fixture.Model.Name = "newer-new-name";
        fixture.Repository.Reader = null;

        pending.SetResult(fixture.Repository.Items);
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        var created = Assert.Single(fixture.Repository.Items, profile => profile.Name == "captured-new-name");
        Assert.Equal(created.Id, fixture.Model.SelectedItem?.Id);
        Assert.Equal("newer-new-name", fixture.Model.Name);
        Assert.True(fixture.Model.IsFormOpen);
        await fixture.Model.SaveCommand.ExecuteAsync(null);
        Assert.Equal(created.Id, Assert.Single(fixture.Repository.Items, profile => profile.Name == "newer-new-name").Id);
        Assert.DoesNotContain(fixture.Repository.Items, profile => profile.Name == "captured-new-name");
        Assert.Equal(2, fixture.Repository.Items.Count);
    }

    [Fact]
    public async Task Delete_ClosingAndReopeningEditorRequiresFreshConfirmation()
    {
        using var fixture = new McpSettingsReloadFixture();
        var original = Assert.Single(fixture.Model.Items);
        fixture.Model.OpenEditFormCommand.Execute(original);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        fixture.Model.CloseFormCommand.Execute(null);
        fixture.Model.OpenEditFormCommand.Execute(original);

        await fixture.Model.DeleteCommand.ExecuteAsync(null);

        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Single(fixture.Repository.Items);
        Assert.Contains("再次点击", fixture.Model.Status);
        await fixture.Model.DeleteCommand.ExecuteAsync(null);
        Assert.Empty(fixture.Repository.Items);
        Assert.False(fixture.Model.IsFormOpen);
    }

    [Fact]
    public async Task Save_LateFailureDoesNotReplaceNewerDraftStatusOrExposeRepositoryDetails()
    {
        using var fixture = new McpSettingsReloadFixture();
        fixture.Model.OpenEditFormCommand.Execute(Assert.Single(fixture.Model.Items));
        var pending = new TaskCompletionSource<IReadOnlyList<McpServerProfile>>(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Repository.Reader = _ => pending.Task;
        var run = fixture.Model.SaveCommand.ExecuteAsync(null);
        fixture.Model.Name = "newer-draft";

        pending.SetException(new IOException("synthetic-private-repository-error"));
        await run.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);

        Assert.Equal("newer-draft", fixture.Model.Name);
        Assert.True(fixture.Model.IsFormOpen);
        Assert.Equal(0, fixture.Repository.WriteCalls);
        Assert.Equal(0, fixture.Manager.ReloadCount);
        Assert.DoesNotContain("synthetic-private", fixture.Model.Status);
        Assert.DoesNotContain("无法保存", fixture.Model.Status);
    }
}
