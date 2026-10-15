using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceSubagentLifetimeFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private SubagentLifetimeModelServer? _server;
    private WorkspaceViewModel? _workspace;

    private WorkspaceSubagentLifetimeFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        _timeout.CancelAfter(TimeSpan.FromSeconds(20));
    }

    public string Root { get; }
    public string ProjectRoot => Path.Combine(Root, "project");
    public CancellationToken Token => _timeout.Token;
    public ServiceProvider Services => _services!;
    public WorkspaceViewModel Workspace => _workspace!;
    public SubagentLifetimeModelServer Server => _server!;
    public IWorkspaceRepository Repository => Services.GetRequiredService<IWorkspaceRepository>();
    public IToolExecutionAuditReader Audits => Services.GetRequiredService<IToolExecutionAuditReader>();

    public static async Task<WorkspaceSubagentLifetimeFixture> CreateAsync(bool chunked = false)
    {
        var fixture = new WorkspaceSubagentLifetimeFixture();
        try
        {
            Directory.CreateDirectory(fixture.ProjectRoot);
            fixture._server = new SubagentLifetimeModelServer(fixture.Token, chunked);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture.Services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelsAsync();
            await fixture.Services.GetRequiredService<WorkspaceProjectService>().CreateAsync("Subagent lifetime", [fixture.ProjectRoot]);
            fixture._workspace = fixture.Services.GetRequiredService<WorkspaceViewModel>();
            await fixture.Workspace.LoadAsync();
            fixture.Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task ConfigureModelsAsync()
    {
        var providers = Services.GetRequiredService<ProviderSettingsViewModel>();
        await providers.RefreshCommand.ExecuteAsync(null);
        providers.OpenCreateFormCommand.Execute(null);
        providers.Name = "subagent-lifetime-fixture";
        providers.Protocol = ProviderProtocol.OpenAiResponses;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-fixture-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = Services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        foreach (var modelId in new[] { "parent-lifetime", "child-lifetime" })
        {
            models.NewModelCommand.Execute(null);
            models.ModelId = modelId;
            models.DisplayName = modelId;
            models.ContextLength = 32000;
            models.MaximumOutputTokens = 1024;
            models.ReasoningEffort = "medium";
            models.IsDefault = modelId == "parent-lifetime";
            await models.SaveModelCommand.ExecuteAsync(null);
            Assert.False(models.HasStatusError, models.Status);
        }
        var subagents = Services.GetRequiredService<SubagentSettingsViewModel>();
        await subagents.RefreshCommand.ExecuteAsync(null);
        subagents.NewCommand.Execute(null);
        subagents.Name = "review";
        subagents.TriggerCondition = "检查代码";
        subagents.SystemPrompt = "请逐步检查代码。";
        subagents.SelectedModel = Assert.Single(subagents.Models, model => model.Model.ModelId == "child-lifetime");
        subagents.ReasoningEffort = "high";
        subagents.ShareContext = true;
        await subagents.SaveCommand.ExecuteAsync(null);
        Assert.Contains("已保存", subagents.Status);
    }

    public async Task StartAsync()
    {
        Workspace.ComposerText = "请使用审查子代理检查代码。";
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
        await WaitUntilAsync(() => Workspace.Messages.Any(message => message.IsToolRunning
            && message.Tool?.Dialogue?.Any(entry => entry.IsPartial && entry.Content == SubagentLifetimeModelServer.PartialReply) == true));
    }

    public async Task ReopenAsync(Guid taskId)
    {
        await Services.DisposeAsync();
        _services = AppComposition.CreateServices(Path.Combine(Root, "data"));
        _workspace = Services.GetRequiredService<WorkspaceViewModel>();
        await Workspace.LoadAsync();
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask?.Id == taskId && Workspace.Messages.Any(message => message.IsSubagentTool));
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
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
        if (_services is not null) await _services.DisposeAsync();
        if (_server is not null) await _server.DisposeAsync();
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
