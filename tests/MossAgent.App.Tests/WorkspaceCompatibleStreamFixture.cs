using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class WorkspaceCompatibleStreamFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private CompatibleStreamFlowServer? _server;
    private WorkspaceViewModel? _workspace;

    private WorkspaceCompatibleStreamFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        ProjectRoot = Path.Combine(Root, "project");
        _timeout.CancelAfter(TimeSpan.FromSeconds(30));
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string FilePath => Path.Combine(ProjectRoot, "evidence.txt");
    public CancellationToken Token => _timeout.Token;
    public WorkspaceViewModel Workspace => _workspace!;
    public CompatibleStreamFlowServer Server => _server!;
    public IWorkspaceRepository Repository => _services!.GetRequiredService<IWorkspaceRepository>();
    public IToolExecutionAuditReader Audits => _services!.GetRequiredService<IToolExecutionAuditReader>();

    public static async Task<WorkspaceCompatibleStreamFixture> CreateAsync(CompatibleStreamScenario scenario)
    {
        var fixture = new WorkspaceCompatibleStreamFixture();
        try
        {
            Directory.CreateDirectory(fixture.ProjectRoot);
            await File.WriteAllTextAsync(fixture.FilePath, CompatibleStreamFlowResponses.Evidence, fixture.Token);
            fixture._server = new CompatibleStreamFlowServer(fixture.FilePath, scenario, fixture.Token);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelAsync();
            await fixture._services.GetRequiredService<WorkspaceProjectService>().CreateAsync("兼容接口验收", [fixture.ProjectRoot]);
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
        providers.Name = "compatible-fixture";
        providers.Protocol = ProviderProtocol.OpenAiCompatible;
        providers.BaseUrl = Server.BaseUri.AbsoluteUri;
        providers.ApiKey = "synthetic-compatible-key";
        providers.IsDefault = true;
        await providers.SaveCommand.ExecuteAsync(null);
        Assert.False(providers.HasValidationError, providers.Status);
        var models = services.GetRequiredService<ModelSettingsViewModel>();
        await models.RefreshProvidersCommand.ExecuteAsync(null);
        models.NewModelCommand.Execute(null);
        models.ModelId = "compatible-model";
        models.DisplayName = "Compatible Model";
        models.ContextLength = 32000;
        models.MaximumOutputTokens = 1024;
        models.ReasoningEffort = "high";
        models.Temperature = 0.1;
        models.TopP = 0.8;
        models.IsDefault = true;
        await models.SaveModelCommand.ExecuteAsync(null);
        Assert.False(models.HasStatusError, models.Status);
    }

    public async Task SendAsync(ApprovalPolicy policy)
    {
        Workspace.SetApprovalPolicyCommand.Execute(policy);
        Workspace.ComposerText = "验证兼容接口的文件工具链路。";
        Assert.True(Workspace.PrimaryActionCommand.CanExecute(null));
        Workspace.PrimaryActionCommand.Execute(null);
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask is not null && !Workspace.IsRunning);
    }

    public async Task ReopenAsync(Guid taskId)
    {
        await _services!.DisposeAsync();
        _services = AppComposition.CreateServices(Path.Combine(Root, "data"));
        _workspace = _services.GetRequiredService<WorkspaceViewModel>();
        await Workspace.LoadAsync();
        await WaitUntilAsync(() => Workspace.Catalog.SelectedTask?.Id == taskId && Workspace.Messages.Count > 0);
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
        using var deletion = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Directory.Exists(Root))
        {
            try { Directory.Delete(Root, recursive: true); return; }
            catch (IOException) when (!deletion.IsCancellationRequested) { await Task.Delay(30, deletion.Token); }
        }
    }
}
