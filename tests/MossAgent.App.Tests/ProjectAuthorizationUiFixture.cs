using System.Security.Cryptography;
using System.Text;
using Avalonia.Styling;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MossAgent.App.Services;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class ProjectAuthorizationUiFixture : IAsyncDisposable
{
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private ProjectAuthorizationModelServer? _server;

    private ProjectAuthorizationUiFixture()
    {
        Root = Directory.CreateTempSubdirectory("MossAgent-project-auth-").FullName;
        _timeout.CancelAfter(TimeSpan.FromSeconds(90));
    }

    public const string InitialContent = "initial sentinel\n";
    public string Root { get; }
    public bool IsExpansion { get; private init; }
    public string DataRoot => Path.Combine(Root, "data");
    public string PrimaryRoot => Path.Combine(Root, "primary");
    public string AdditionalRoot => Path.Combine(Root, "additional");
    public string OtherRoot => Path.Combine(Root, "other-project");
    public string PrimaryFile => Path.Combine(PrimaryRoot, "fixture.txt");
    public string AdditionalFile => Path.Combine(AdditionalRoot, "fixture.txt");
    public string OtherFile => Path.Combine(OtherRoot, "fixture.txt");
    public CancellationToken Token => _timeout.Token;
    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public IWorkspaceRepository Repository => _services!.GetRequiredService<IWorkspaceRepository>();
    public IToolExecutionAuditReader Audits => _services!.GetRequiredService<IToolExecutionAuditReader>();
    public ProjectAuthorizationModelServer Server => _server!;
    public ProjectEditorPickerProbe Picker { get; } = new();
    public MainWindow Window { get; private set; } = null!;
    public ProjectProfile OtherProject { get; private set; } = null!;
    public WorkspaceProjectEditorViewModel Editor => Main.Workspace.Catalog.ProjectEditor;

    public static async Task<ProjectAuthorizationUiFixture> CreateAsync(bool dark, bool compact, bool expansion = false)
    {
        var fixture = new ProjectAuthorizationUiFixture { IsExpansion = expansion };
        try
        {
            foreach (var root in new[] { fixture.PrimaryRoot, fixture.AdditionalRoot, fixture.OtherRoot })
            {
                Directory.CreateDirectory(root);
                await File.WriteAllTextAsync(Path.Combine(root, "fixture.txt"), InitialContent, fixture.Token);
            }
            IReadOnlyList<IReadOnlyList<string>> paths = expansion
                ? [[fixture.PrimaryFile, fixture.AdditionalFile], [fixture.AdditionalFile]]
                : [[fixture.PrimaryFile], [fixture.PrimaryFile], [fixture.PrimaryFile]];
            fixture._server = new ProjectAuthorizationModelServer(paths,
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(InitialContent))), fixture.Token, cancelFirst: expansion);
            fixture._services = fixture.CreateServices();
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.ConfigureModelAsync();
            fixture.OtherProject = new ProjectProfile(Guid.NewGuid(), "独立项目", fixture.OtherRoot,
                [fixture.OtherRoot], DateTimeOffset.UtcNow);
            await fixture.Repository.SaveProjectAsync(fixture.OtherProject, fixture.Token);
            await fixture.Main.Workspace.LoadAsync();
            fixture.Show(dark, compact);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private ServiceProvider CreateServices() => AppComposition.CreateServiceCollection(DataRoot)
        .Replace(ServiceDescriptor.Singleton<IFolderPickerService>(Picker)).BuildServiceProvider(validateScopes: true);

    private async Task ConfigureModelAsync()
    {
        var configurations = _services!.GetRequiredService<IConfigurationRepository>();
        var provider = new AiProvider(Guid.NewGuid(), "project-fixture", ProviderProtocol.OpenAiResponses,
            Server.BaseUri, "synthetic-project-secret", null, true, true, new Dictionary<string, string>());
        await configurations.SaveProviderAsync(provider, Token);
        await configurations.SaveModelAsync(new ModelProfile(Guid.NewGuid(), provider.Id, "project-model", "Project Model",
            128000, 1024, "off", null, null, false, false, true, true, "{}"), Token);
    }

    private void Show(bool dark, bool compact)
    {
        Window = new MainWindow
        {
            DataContext = Main,
            Width = compact ? 526 : 1280,
            Height = compact ? 601 : 820,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
        };
        Window.Show();
        ProviderUiWorkflowInput.Flush(Window);
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            ProviderUiWorkflowInput.Flush(Window);
            if (Server.Completion.IsFaulted) await Server.Completion;
            await Task.Delay(15, Token);
        }
        ProviderUiWorkflowInput.Flush(Window);
    }

    public async Task ReopenAsync(Guid taskId)
    {
        var dark = Window.RequestedThemeVariant == ThemeVariant.Dark;
        var compact = Window.ClientSize.Width < 800;
        Window.Close();
        await _services!.DisposeAsync();
        _services = CreateServices();
        await Main.Workspace.LoadAsync();
        Show(dark, compact);
        await ProjectAuthorizationUiInput.SelectTaskAsync(this, taskId);
        Assert.False(Main.Workspace.HasActiveRun);
        Assert.False(Main.Approval.IsPending);
    }

    public async Task AssertResultAsync(Guid taskId, string callId, string? error, bool? approved)
    {
        var history = await Repository.GetMessagesAsync(taskId, Token);
        var tool = WorkspaceConversationMapper.ToChatMessage(Assert.Single(history,
            message => message.ToolCallId == callId)).Tool!;
        Assert.Equal(error, tool.ErrorCode);
        Assert.Equal(error is null, tool.IsSuccess);
        var audit = Assert.Single(await Audits.GetRecentAsync(taskId, 20, Token), item => item.CallId == callId);
        Assert.Equal("filesystem.write_file", audit.ToolName);
        Assert.Equal(ApprovalPolicy.AskEveryTime, audit.ApprovalPolicy);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(error is null, audit.IsSuccess);
        Assert.NotNull(audit.CompletedAt);
        var visible = Assert.Single(Main.Workspace.Messages, message => message.ToolCallId == callId);
        Assert.Equal(tool.ErrorCode, visible.Tool!.ErrorCode);
        Assert.Equal(tool.FileChanges, visible.Tool.FileChanges);
        Assert.DoesNotContain("synthetic-project-secret", System.Text.Json.JsonSerializer.Serialize(history));
    }

    public async Task AssertSentinelsAsync(bool primaryChanged, bool additionalChanged = false)
    {
        Assert.Equal(primaryChanged ? ProjectAuthorizationModelServer.UpdatedContent : InitialContent,
            await File.ReadAllTextAsync(PrimaryFile, Token));
        Assert.Equal(additionalChanged ? ProjectAuthorizationModelServer.UpdatedContent : InitialContent,
            await File.ReadAllTextAsync(AdditionalFile, Token));
        Assert.Equal(InitialContent, await File.ReadAllTextAsync(OtherFile, Token));
    }

    public async Task<IReadOnlyList<ToolExecutionAuditRecord>> CaptureAuditsAsync(Guid taskId)
    {
        await WaitUntilAsync(() => Main.Workspace.FileChanges.Statistics.HasTotals || Main.Workspace.FileChanges.Statistics.HasError);
        var audits = await Audits.GetRecentAsync(taskId, 20, Token);
        Assert.All(audits, audit => Assert.NotNull(audit.CompletedAt));
        return audits;
    }

    public async Task AssertRestoredAuditsAsync(Guid taskId, IReadOnlyList<ToolExecutionAuditRecord> recorded)
    {
        var restored = await CaptureAuditsAsync(taskId);
        foreach (var audit in recorded) Assert.Contains(audit, restored);
        Assert.All(restored.Where(audit => !recorded.Any(previous => previous.Id == audit.Id)), audit =>
        {
            Assert.Equal("filesystem.change_statistics", audit.ToolName);
            Assert.Equal(ToolRiskLevel.ReadOnly, audit.RiskLevel);
            Assert.Equal(IsExpansion, audit.IsSuccess);
            Assert.Equal(IsExpansion, audit.ApprovalGranted);
            Assert.Equal(IsExpansion ? null : "path_denied", audit.ErrorCode);
            Assert.NotNull(audit.CompletedAt);
        });
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            Window?.Close();
            if (_services is not null) await _services.DisposeAsync();
        }
        finally
        {
            if (_server is not null) await _server.DisposeAsync();
            await _timeout.CancelAsync();
            SqliteTestPool.Clear(DataRoot);
            var target = Path.GetFullPath(Root);
            if (!target.StartsWith(Path.GetFullPath(Path.GetTempPath()) + "MossAgent-project-auth-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected project authorization fixture root.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            _timeout.Dispose();
        }
    }
}
