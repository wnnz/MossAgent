using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Authorization;
using MossAgent.Infrastructure.Persistence;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuthorizationResourceConfirmationWorkflowTests
{
    [Fact]
    public async Task RenewedResourceBetweenConfirmationsLeavesTheEntireSelectionOutstanding()
    {
        await using var fixture = await WorkspaceFileEditingFixture.CreateAsync(false, "unused");
        var services = fixture.Services;
        var project = fixture.Workspace.Catalog.SelectedProject!;
        var registry = services.GetRequiredService<IAuthorizationResourceRegistry>();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await registry.RegisterAsync(first, project.Id, project.AuthorizationRevision, fixture.Token);
        await registry.RegisterAsync(second, project.Id, project.AuthorizationRevision, fixture.Token);
        await ExpireAsync(services.GetRequiredService<SqliteConnectionFactory>(), first, second, fixture.Token);
        using var editor = new WorkspaceProjectEditorViewModel(services.GetRequiredService<WorkspaceProjectService>());
        editor.Edit(project);
        await WaitUntilAsync(() => editor.AuthorizationResources.StaleCount == 2, fixture.Token);
        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);
        Assert.Contains("确认", editor.AuthorizationResources.AbandonLabel);
        await registry.HeartbeatAsync(second, fixture.Token);

        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);

        Assert.True(editor.AuthorizationResources.HasError);
        Assert.Equal(2, (await registry.ListOutstandingAsync(project.Id, fixture.Token)).Count);
        await editor.AuthorizationResources.RefreshCommand.ExecuteAsync(null);
        Assert.False(editor.AuthorizationResources.HasError);
        Assert.Equal(2, editor.AuthorizationResources.OutstandingCount);
        Assert.Equal(1, editor.AuthorizationResources.StaleCount);
        Assert.DoesNotContain("再次", editor.AuthorizationResources.AbandonLabel);
        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);
        Assert.Equal(2, (await registry.ListOutstandingAsync(project.Id, fixture.Token)).Count);
        await editor.AuthorizationResources.AbandonCommand.ExecuteAsync(null);
        Assert.Equal(second, Assert.Single(await registry.ListOutstandingAsync(project.Id, fixture.Token)).LeaseId);
        Assert.Equal(1, editor.AuthorizationResources.OutstandingCount);
        Assert.False(editor.AuthorizationResources.HasStaleResources);
        Assert.Empty(fixture.Server.Requests);
        await registry.ReleaseAsync(second, fixture.Token);
    }

    private static async Task ExpireAsync(SqliteConnectionFactory connections, Guid first, Guid second,
        CancellationToken token)
    {
        await using var connection = connections.Create();
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE authorization_resource_leases SET heartbeat_at=$time WHERE lease_id IN ($first,$second);";
        command.Parameters.AddWithValue("$time", DateTimeOffset.UtcNow.AddMinutes(-2).ToString("O"));
        command.Parameters.AddWithValue("$first", first.ToString());
        command.Parameters.AddWithValue("$second", second.ToString());
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        while (!predicate()) await Task.Delay(15, deadline.Token);
    }
}
