using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Infrastructure.Persistence;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class SubagentProtocolFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private WorkspaceViewModel? _workspace;

    private SubagentProtocolFixture(ProviderProtocol protocol, bool denyPath, ProviderProtocol parentProtocol)
    {
        Protocol = protocol;
        ParentProtocol = parentProtocol;
        Root = Directory.CreateTempSubdirectory("MossAgent-subagent-protocol-").FullName;
        ProjectRoot = Path.Combine(Root, "project");
        _timeout.CancelAfter(TimeSpan.FromSeconds(30));
        ParentServer = new WorkspaceModelStreamServer(parentProtocol == ProviderProtocol.GeminiGenerateContent
            ? GeminiParentSubagentResponses.Parent : SubagentProtocolResponses.Parent, Token,
            parentProtocol == ProviderProtocol.GeminiGenerateContent ? "v1beta/" : "v1/");
        ChildServer = new WorkspaceModelStreamServer((request, turn) =>
            SubagentProtocolResponses.Child(request, turn, Protocol, denyPath ? OutsideFile : AllowedFile), Token,
            protocol == ProviderProtocol.GeminiGenerateContent ? "v1beta/" : "v1/");
    }

    public string Root { get; }
    public string ProjectRoot { get; }
    public string AllowedFile => Path.Combine(ProjectRoot, "evidence.txt");
    public string OutsideFile => Path.Combine(Root, "outside.txt");
    public ProviderProtocol Protocol { get; }
    public ProviderProtocol ParentProtocol { get; }
    public string ParentModelId => ParentProtocol == ProviderProtocol.GeminiGenerateContent ? "gemini-3-flash-preview" : "parent-model";
    public CancellationToken Token => _timeout.Token;
    public WorkspaceModelStreamServer ParentServer { get; }
    public WorkspaceModelStreamServer ChildServer { get; }
    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public WorkspaceViewModel Workspace => _workspace!;
    public IConfigurationRepository Configurations => _services!.GetRequiredService<IConfigurationRepository>();
    public ISubagentConfigurationRepository Subagents => _services!.GetRequiredService<ISubagentConfigurationRepository>();
    public IWorkspaceRepository Repository => _services!.GetRequiredService<IWorkspaceRepository>();
    public IToolExecutionAuditReader Audits => _services!.GetRequiredService<IToolExecutionAuditReader>();
    public AiProvider ChildProvider { get; private set; } = null!;
    public ModelProfile ChildModel { get; private set; } = null!;

    public static async Task<SubagentProtocolFixture> CreateAsync(
        ProviderProtocol protocol, bool shareContext, bool denyPath = false,
        ProviderProtocol parentProtocol = ProviderProtocol.OpenAiResponses)
    {
        var fixture = new SubagentProtocolFixture(protocol, denyPath, parentProtocol);
        try
        {
            Directory.CreateDirectory(fixture.ProjectRoot);
            await File.WriteAllTextAsync(fixture.AllowedFile, SubagentProtocolResponses.Evidence, fixture.Token);
            await File.WriteAllTextAsync(fixture.OutsideFile, SubagentProtocolResponses.PrivateEvidence, fixture.Token);
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture.Root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureAsync(shareContext);
            await fixture._services.GetRequiredService<WorkspaceProjectService>().CreateAsync("子代理跨协议验收", [fixture.ProjectRoot]);
            fixture._workspace = fixture._services.GetRequiredService<WorkspaceViewModel>();
            await fixture.Workspace.LoadAsync();
            return fixture;
        }
        catch { await fixture.DisposeAsync(); throw; }
    }

    private async Task ConfigureAsync(bool shareContext)
    {
        var parent = await AddProviderAsync("parent-provider", ParentProtocol, ParentServer.BaseUri, true);
        await AddModelAsync(parent, ParentModelId, true);
        ChildProvider = await AddProviderAsync("child-provider", Protocol, ChildServer.BaseUri, false);
        ChildModel = await AddModelAsync(ChildProvider, Protocol switch
        {
            ProviderProtocol.GeminiGenerateContent => "gemini-3-flash-preview",
            ProviderProtocol.AnthropicMessages => "claude-sonnet-4-5",
            _ => "child-model"
        }, false);
        var settings = Main.Subagents;
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "protocol-review";
        settings.TriggerCondition = "PROFILE_TRIGGER_MARKER";
        settings.SystemPrompt = "CHILD_SYSTEM_MARKER";
        settings.SelectedModel = Assert.Single(settings.Models, item => item.Model.Id == ChildModel.Id);
        settings.ReasoningEffort = "high";
        settings.ShareContext = shareContext;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError, settings.Status);
    }

    private async Task<AiProvider> AddProviderAsync(string name, ProviderProtocol protocol, Uri endpoint, bool isDefault)
    {
        var settings = Main.Providers;
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.OpenCreateFormCommand.Execute(null);
        settings.Name = name;
        settings.Protocol = protocol;
        settings.BaseUrl = endpoint.AbsoluteUri;
        settings.ApiKey = "synthetic-only-key";
        settings.IsDefault = isDefault;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.False(settings.HasValidationError, settings.Status);
        return Assert.Single(settings.Items, item => item.Name == name);
    }

    private async Task<ModelProfile> AddModelAsync(AiProvider provider, string id, bool isDefault)
    {
        var settings = Main.Models;
        await settings.RefreshProvidersCommand.ExecuteAsync(null);
        settings.SelectedProvider = Assert.Single(settings.Providers, item => item.Id == provider.Id);
        settings.NewModelCommand.Execute(null);
        settings.ModelId = id;
        settings.DisplayName = id;
        settings.ContextLength = 32000;
        settings.MaximumOutputTokens = 4096;
        settings.ReasoningEffort = "medium";
        settings.IsDefault = isDefault;
        await settings.SaveModelCommand.ExecuteAsync(null);
        Assert.False(settings.HasStatusError, settings.Status);
        return Assert.Single(await Configurations.GetModelsAsync(provider.Id, Token));
    }

    public async Task DisableChildAsync(bool provider)
    {
        if (provider)
        {
            Main.Providers.EditProviderCommand.Execute(ChildProvider);
            Main.Providers.IsEnabled = false;
            await Main.Providers.SaveCommand.ExecuteAsync(null);
            Assert.False(Main.Providers.HasValidationError, Main.Providers.Status);
        }
        else
        {
            Main.Models.EditProviderModelCommand.Execute(ChildModel);
            Main.Models.IsEnabled = false;
            await Main.Models.SaveModelCommand.ExecuteAsync(null);
            Assert.False(Main.Models.HasStatusError, Main.Models.Status);
        }
    }

    public async Task SendAsync()
    {
        Assert.Equal(ParentModelId, Workspace.SelectedModel!.ModelId);
        Workspace.SetApprovalPolicyCommand.Execute(ApprovalPolicy.FullAccess);
        Workspace.ComposerText = "审查合成文件 " + SubagentProtocolResponses.ParentMarker;
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
        Workspace.Catalog.SelectedTask = Assert.Single(await Repository.GetTasksAsync(
            Workspace.Catalog.SelectedProject!.Id, Token), task => task.Id == taskId);
        await WaitUntilAsync(() => Workspace.Messages.Any(message => message.IsSubagentTool));
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
        await ParentServer.DisposeAsync();
        await ChildServer.DisposeAsync();
        if (_services is not null) await _services.DisposeAsync();
        _timeout.Dispose();
        using var connection = new SqliteConnectionFactory(new AppDataPaths(Path.Combine(Root, "data"))).Create();
        SqliteConnection.ClearPool(connection);
        using var deletion = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while (Directory.Exists(Root))
        {
            try { Directory.Delete(Root, recursive: true); return; }
            catch (IOException) when (!deletion.IsCancellationRequested) { await Task.Delay(30, deletion.Token); }
        }
    }
}
