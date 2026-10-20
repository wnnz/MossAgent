using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceWorktreeReferenceTests
{
    [Theory]
    [InlineData("selected-base")]
    [InlineData("selected-tag")]
    [InlineData("commit")]
    public async Task ExplicitBaseReachesActualAgentReadFollowUpAndRestoredWorktree(string selection)
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var workspace = fixture.Workspace;
        var original = (await owner.GitAsync(["rev-parse", "HEAD"])).StandardOutput.Trim();
        await owner.GitAsync(["branch", "selected-base", original]);
        await owner.GitAsync(["tag", "selected-tag", original]);
        var source = Path.Combine(owner.Project.PrimaryDirectory, "README.md");
        await File.WriteAllTextAsync(source, "later committed baseline\n", fixture.Token);
        await owner.GitAsync(["add", "--", "README.md"]);
        await owner.GitAsync(["-c", "commit.gpgsign=false", "commit", "-m", "later baseline"]);
        await File.WriteAllTextAsync(source, "uncommitted source change\n", fixture.Token);
        fixture.Server.ResponseOverride = ReadBaseline;
        workspace.Catalog.Environment.BaseReference = selection == "commit" ? original : selection;
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 2 && !workspace.IsRunning);
        await fixture.AssertPersistedAsync(AgentTaskStatus.Completed);
        var task = Assert.IsType<AgentTask>(workspace.Catalog.SelectedTask);
        var path = Assert.IsType<string>(task.WorktreePath);
        Assert.Equal(original, (await owner.GitAsync(["rev-parse", "HEAD"], path)).StandardOutput.Trim());
        Assert.Equal("committed baseline\n", (await File.ReadAllTextAsync(Path.Combine(path, "README.md"), fixture.Token)).ReplaceLineEndings("\n"));
        Assert.Equal("uncommitted source change\n", await File.ReadAllTextAsync(source, fixture.Token));
        Assert.Contains("committed baseline", fixture.Server.Requests[1].GetRawText());
        Assert.DoesNotContain("uncommitted source change", fixture.Server.Requests[1].GetRawText());
        var audit = Assert.Single(await fixture.ToolAudits.GetRecentAsync(task.Id, 10, fixture.Token));
        Assert.Equal("read-selected-base", audit.CallId);
        Assert.True(audit.IsSuccess);
        var context = fixture.Panels.CreateContext(task, owner.Project);
        Assert.Equal(path, context.WorkingDirectory);
        Assert.DoesNotContain(owner.Project.PrimaryDirectory, context.AuthorizedRoots);
        workspace.ComposerText = "continue selected base";
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 3 && !workspace.IsRunning);
        Assert.Equal(path, workspace.Catalog.SelectedTask?.WorktreePath);
        await using var services = AppComposition.CreateServices(fixture.DataRoot);
        var restored = services.GetRequiredService<MainWindowViewModel>().Workspace;
        await restored.LoadAsync();
        restored.Catalog.SelectedTask = Assert.Single(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        await fixture.WaitUntilAsync(() => restored.Messages.Count >= 4);
        Assert.Equal(path, restored.Catalog.SelectedTask?.WorktreePath);
        Assert.False(restored.Catalog.Environment.CanChoose);
        Assert.Equal(3, fixture.Server.Requests.Count);
    }

    [Theory]
    [InlineData("missing-base")]
    [InlineData("HEAD^{tree}")]
    [InlineData("--help")]
    public async Task RefusalPreservesDraftAndReferenceWithoutTaskOrModelRequest(string reference)
    {
        await using var owner = await WorkspaceWorktreeGitFixture.CreateAsync();
        var fixture = owner.Fixture;
        var workspace = fixture.Workspace;
        var prompt = workspace.ComposerText;
        workspace.Catalog.Environment.BaseReference = reference;
        workspace.Catalog.Environment.UseWorktreeCommand.Execute(null);
        workspace.PrimaryActionCommand.Execute(null);
        await fixture.WaitUntilAsync(() => workspace.HasSendPreparationError);
        Assert.False(workspace.IsRunning);
        Assert.Null(workspace.Catalog.SelectedTask);
        Assert.Equal(prompt, workspace.ComposerText);
        Assert.Equal(reference, workspace.Catalog.Environment.BaseReference);
        Assert.Equal(WorkspaceText.Get("TaskEnvironmentBaseReferenceFailure"), workspace.Catalog.Environment.PreparationNotice);
        Assert.Empty(await fixture.Tasks.GetTasksAsync(owner.Project.Id, fixture.Token));
        Assert.Empty(fixture.Server.Requests);
    }

    private static string? ReadBaseline(JsonElement request, int number)
    {
        if (number != 1) return null;
        var name = request.GetProperty("tools").EnumerateArray().Select(tool => tool.GetProperty("name").GetString())
            .Single(value => value?.StartsWith("filesystem_read_file__", StringComparison.Ordinal) == true);
        return JsonSerializer.Serialize(new { type = "response.output_item.done", item = new
        { type = "function_call", call_id = "read-selected-base", name, arguments = "{\"path\":\"README.md\"}" } });
    }
}
