using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceComposerPersistenceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnsentTextSurvivesShutdownAndAnIndependentApplicationComposition(bool withProject)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        var workspace = fixture.Workspace;
        const string text = "  关闭后需要恢复的草稿\r\n包括空白。  ";
        workspace.ComposerText = text;
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();

        await using var restartedServices = AppComposition.CreateServices(fixture.DataRoot);
        await restartedServices.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restartedServices.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();

        Assert.Equal(text, reopened.ComposerText);
        Assert.Equal(workspace.Catalog.SelectedProject?.Id, reopened.Catalog.SelectedProject?.Id);
        Assert.Null(reopened.Catalog.SelectedTask);
        Assert.Empty(fixture.Server.Requests);
        await reopened.ShutdownAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualSendClearsOnlyItsSourceAndNextDraftRestoresInTheCorrectTask(bool withProject)
    {
        await using var fixture = await WorkspaceModelSelectionFixture.CreateAsync(withProject: withProject);
        var workspace = fixture.Workspace;
        var source = workspace.Drafts.Scope;
        workspace.ComposerText = "实际发送的正文";
        await workspace.Drafts.FlushAsync();
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !workspace.IsRunning);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        workspace.ComposerText = "不应自动发送的下一条草稿";
        await workspace.Drafts.FlushAsync();
        var target = new ConversationDraftScope(task.ProjectId, task.Id);
        Assert.Equal(string.Empty, (await fixture.Drafts.GetAsync(source, fixture.Token))!.Text);
        Assert.Equal(workspace.ComposerText, (await fixture.Drafts.GetAsync(target, fixture.Token))!.Text);
        workspace.Catalog.NewTaskCommand.Execute(null);
        Assert.Empty(workspace.ComposerText);
        workspace.Catalog.SelectTaskCommand.Execute(task);
        await workspace.Drafts.LoadCurrentAsync();
        Assert.Equal("不应自动发送的下一条草稿", workspace.ComposerText);
        await workspace.ShutdownAsync();
        await fixture.Scheduler.StopAsync();

        await using var restartedServices = AppComposition.CreateServices(fixture.DataRoot);
        await restartedServices.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
        var reopened = restartedServices.GetRequiredService<WorkspaceViewModel>();
        await reopened.LoadAsync();
        reopened.Catalog.SelectTaskCommand.Execute(task);
        await reopened.Drafts.LoadCurrentAsync();

        Assert.Equal("不应自动发送的下一条草稿", reopened.ComposerText);
        Assert.Equal(task.Id, reopened.Catalog.SelectedTask?.Id);
        var input = Assert.Single(fixture.Server.Requests).GetProperty("input").EnumerateArray().ToArray();
        Assert.Contains(input, message => message.GetProperty("role").GetString() == "user"
            && message.GetProperty("content").GetString() == "实际发送的正文");
        Assert.DoesNotContain(input, message => message.GetProperty("content").GetString()?.Contains("不应自动发送", StringComparison.Ordinal) == true);
        var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
        Assert.Contains(messages, message => message.Role == MessageRole.User && message.Content == "实际发送的正文");
        Assert.DoesNotContain(messages, message => message.Content.Contains("不应自动发送", StringComparison.Ordinal));
        await reopened.ShutdownAsync();
    }
}
