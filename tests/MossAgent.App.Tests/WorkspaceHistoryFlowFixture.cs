using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceHistoryFlowFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private HistoryContinuationModelServer? _server;
    private WorkspaceViewModel? _workspace;

    private WorkspaceHistoryFlowFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        ProjectRoot = Path.Combine(Root, "project");
        _timeout.CancelAfter(TimeSpan.FromSeconds(20));
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string FilePath => Path.Combine(ProjectRoot, "evidence.txt");
    public CancellationToken Token => _timeout.Token;
    public WorkspaceViewModel Workspace => _workspace!;
    public HistoryContinuationModelServer Server => _server!;
    public ApprovalViewModel Approval => _services!.GetRequiredService<ApprovalViewModel>();
    public IWorkspaceRepository Repository => _services!.GetRequiredService<IWorkspaceRepository>();
    public IToolExecutionAuditReader Audits => _services!.GetRequiredService<IToolExecutionAuditReader>();

    public static async Task<WorkspaceHistoryFlowFixture> CreateAsync(bool mutation)
    {
        var fixture = new WorkspaceHistoryFlowFixture();
        try
        {
            Directory.CreateDirectory(fixture.ProjectRoot);
            await File.WriteAllTextAsync(fixture.FilePath, HistoryContinuationModelServer.Evidence, fixture.Token);
            fixture._server = new HistoryContinuationModelServer(fixture.FilePath, mutation, fixture.Token);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelAsync();
            await fixture._services.GetRequiredService<WorkspaceProjectService>().CreateAsync("History fixture", [fixture.ProjectRoot]);
            fixture._workspace = fixture._services.GetRequiredService<WorkspaceViewModel>();
            await fixture.Workspace.LoadAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task ConfigureModelAsync()
    {
        var services = _services!;
        var providers = services.GetRequiredService<ProviderSettingsViewModel>();
        await providers.RefreshCommand.ExecuteAsync(null);
        providers.OpenCreateFormCommand.Execute(null);
        providers.Name = "history-fixture";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.NewModelCommand.Execute(null);
        models.ModelId = "history-model";
        models.DisplayName = "History Model";
        models.ContextLength = 32000;
        models.MaximumOutputTokens = 1024;
        models.ReasoningEffort = "off";
        models.IsDefault = true;
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(models.HasStatusError, models.Status);
    }

    public async Task SendAsync(string prompt)
    {
        Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.ReadOnly);
        Workspace.ComposerText = prompt;
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask is not null && !Workspace.IsRunning);
        Assert.Equal(AgentTaskStatus.Completed, Workspace.Catalog.SelectedTask!.Status);
    }

    public async Task ReopenAsync(Guid taskId)
    {
        await _services!.DisposeAsync();
        _services = AppComposition.CreateServices(Path.Combine(Root, "data"));
        _workspace = _services.GetRequiredService<WorkspaceViewModel>();
        await Workspace.LoadAsync();
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask?.Id == taskId && Workspace.Messages.Any(message => message.IsTool));
    }

    private async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate()) await Task.Delay(15, Token);
    }

    public async ValueTask DisposeAsync()
    {
        if (_workspace is { IsRunning: true })
        {
            _workspace.PrimaryActionCommand.Execute(null);
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (_workspace.IsRunning) await Task.Delay(15, cleanup.Token);
        }
        if (_server is not null) await _server.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
        _timeout.Dispose();
        SqliteTestPool.Clear(Path.Combine(Root, "data"));
        await DeleteFixtureAsync();
    }

    private async Task DeleteFixtureAsync()
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Directory.Exists(Root))
        {
            try { Directory.Delete(Root, recursive: true); return; }
            catch (IOException) when (!cleanup.IsCancellationRequested)
            {
                await Task.Delay(30, cleanup.Token);
            }
        }
    }
}
