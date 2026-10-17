using System.Text.Json;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using MossAgent.App.Views;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Mcp;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

internal sealed class McpUiRecoveryFixture : IAsyncDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
    private readonly CancellationTokenSource _timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private ServiceProvider? _services;
    private WorkspaceMcpHttpServer? _server;
    private WorkspaceToolCallModelServer? _model;

    private McpUiRecoveryFixture() => _timeout.CancelAfter(TimeSpan.FromSeconds(60));

    public CancellationToken Token => _timeout.Token;
    public MainWindowViewModel Main => _services!.GetRequiredService<MainWindowViewModel>();
    public IMcpManager Manager => _services!.GetRequiredService<IMcpManager>();
    public IToolCatalog Tools => _services!.GetRequiredService<IToolCatalog>();
    public WorkspaceMcpHttpServer Server => _server!;
    public WorkspaceToolCallModelServer Model => _model!;
    public MainWindow Window { get; private set; } = null!;
    public string ToolName { get; private set; } = string.Empty;

    public static async Task<McpUiRecoveryFixture> CreateAsync(bool dark, bool compact)
    {
        var fixture = new McpUiRecoveryFixture();
        try
        {
            Directory.CreateDirectory(fixture._root);
            fixture._server = new WorkspaceMcpHttpServer();
            fixture._services = AppComposition.CreateServices(Path.Combine(fixture._root, "data"));
            await fixture._services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(fixture.Token);
            await fixture.Main.Workspace.LoadAsync();
            fixture.Window = new MainWindow
            {
                DataContext = fixture.Main,
                Width = compact ? 526 : 1280,
                Height = compact ? 601 : 820,
                RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light
            };
            fixture.Window.Show();
            await fixture.ConfigureMcpAsync();
            fixture._model = new WorkspaceToolCallModelServer(
                $"mcp.recovery.{Assert.Single(fixture.Main.Mcp.Items).Id:N}",
                JsonSerializer.SerializeToElement(new { message = "重连后的真实 MCP 回复" }), fixture.Token,
                useNamespacePrefix: true, conversations: 2);
            await fixture.ConfigureWorkspaceAsync();
            fixture.Main.CloseSettingsCommand.Execute(null);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            return fixture;
        }
        catch
        {
            await fixture.DisposeAsync();
            throw;
        }
    }

    private async Task ConfigureMcpAsync()
    {
        Main.SelectSettingsTabCommand.Execute("3");
        ProviderUiWorkflowInput.Flush(Window);
        PressCommand(Main.Mcp.OpenCreateFormCommand);
        ProviderUiWorkflowInput.Type(Window, ProviderUiWorkflowInput.Named<AppInput>(Window, "Server 名称"), "recovery");
        var transport = ProviderUiWorkflowInput.Named<AppComboBox>(Window, "传输协议");
        transport.BringIntoView();
        ProviderUiWorkflowInput.Flush(Window);
        Assert.True(transport.Focus(NavigationMethod.Tab));
        Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        Window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        ProviderUiWorkflowInput.Flush(Window);
        Assert.Equal(McpTransportKind.StreamableHttp, Main.Mcp.Transport);
        ProviderUiWorkflowInput.Type(Window, ProviderUiWorkflowInput.Named<AppInput>(Window, "Streamable HTTP URL"), Server.Endpoint.AbsoluteUri);
        ProviderUiWorkflowInput.Type(Window, ProviderUiWorkflowInput.Named<AppInput>(Window, "HTTP 请求头 JSON 对象"),
            "{\"X-Moss-Fixture\":\"workflow-header\"}");
        SaveReview("editor");
        PressCommand(Main.Mcp.SaveCommand);
        await Main.Mcp.SaveCommand.ExecutionTask!.WaitAsync(Token);
        ProviderUiWorkflowInput.Flush(Window);
        Assert.False(Main.Mcp.IsFormOpen, Main.Mcp.Status);
        Assert.Empty(Manager.Errors);
        ToolName = Assert.Single(Tools.Descriptors, descriptor => descriptor.Name.StartsWith("mcp.recovery.", StringComparison.Ordinal)).Name;
    }

    private async Task ConfigureWorkspaceAsync()
    {
        var repository = _services!.GetRequiredService<IConfigurationRepository>();
        var provider = new AiProvider(Guid.NewGuid(), "recovery", ProviderProtocol.OpenAiResponses,
            Model.BaseUri, "synthetic-key", null, true, true, new Dictionary<string, string>());
        await repository.SaveProviderAsync(provider, Token);
        await repository.SaveModelAsync(new ModelProfile(Guid.NewGuid(), provider.Id, "recovery-model", "Recovery Model",
            128000, 1024, "off", null, null, false, false, true, true, "{}"), Token);
        var project = Path.Combine(_root, "project");
        Directory.CreateDirectory(project);
        await _services!.GetRequiredService<WorkspaceProjectService>().CreateAsync("MCP recovery", [project]);
        await Main.Workspace.LoadAsync();
        Assert.NotNull(Main.Workspace.SelectedModel);
        Assert.NotNull(Main.Workspace.Catalog.SelectedProject);
    }

    public void PressCommand(ICommand command)
    {
        ProviderUiWorkflowInput.Flush(Window);
        SubagentApprovalInteraction.Activate(Window, Assert.Single(Window.GetVisualDescendants().OfType<AppButton>(),
            button => button.IsEffectivelyVisible && ReferenceEquals(button.Command, command)), keyboard: true);
    }

    public void Start(ApprovalPolicy policy = ApprovalPolicy.AskEveryTime)
    {
        Main.CloseSettingsCommand.Execute(null);
        Main.Workspace.Catalog.NewTaskCommand.Execute(null);
        Main.Workspace.SetApprovalPolicyCommand.Execute(policy);
        ProviderUiWorkflowInput.Flush(Window);
        var composer = Assert.Single(Window.GetVisualDescendants().OfType<AppInput>(), input => input.Classes.Contains("composer-input"));
        ProviderUiWorkflowInput.Type(Window, composer, "调用验收 MCP 工具，不访问其他目录。");
        ProviderUiWorkflowInput.Press(Window, ProviderUiWorkflowInput.SendButton(Window));
    }

    public async Task ReconnectAsync()
    {
        Main.SelectSettingsTabCommand.Execute("3");
        PressCommand(Main.Mcp.ReloadCommand);
        await Main.Mcp.ReloadCommand.ExecutionTask!.WaitAsync(Token);
        ProviderUiWorkflowInput.Flush(Window);
    }

    public async Task WaitUntilAsync(Func<bool> predicate)
    {
        while (!predicate())
        {
            ProviderUiWorkflowInput.Flush(Window);
            await Task.Delay(15, Token);
        }
        ProviderUiWorkflowInput.Flush(Window);
    }

    public void SaveReview(string stage)
    {
        var root = Environment.GetEnvironmentVariable("MOSSAGENT_MCP_RECOVERY_QA_ROOT");
        if (string.IsNullOrWhiteSpace(root)) return;
        Directory.CreateDirectory(root);
        ProviderUiWorkflowInput.Flush(Window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = Window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(root, $"{Window.ActualThemeVariant}-{Window.ClientSize.Width}-{stage}.png"), PngBitmapEncoderOptions.Default);
    }

    public async Task AssertPersistedAsync(string? error, bool? approved)
    {
        var taskId = Main.Workspace.Catalog.SelectedTask!.Id;
        await using var reopened = AppComposition.CreateServices(Path.Combine(_root, "data"));
        var repository = reopened.GetRequiredService<IWorkspaceRepository>();
        Assert.Equal(AgentTaskStatus.Completed, Assert.Single(await repository.GetTasksAsync(
            Main.Workspace.Catalog.SelectedProject!.Id, Token), task => task.Id == taskId).Status);
        var history = await repository.GetMessagesAsync(taskId, Token);
        var tool = WorkspaceConversationMapper.ToChatMessage(Assert.Single(history, message => message.ToolCallId == "mcp-flow")).Tool!;
        Assert.Equal(error, tool.ErrorCode);
        Assert.Equal(error is null, tool.IsSuccess);
        var audit = Assert.Single(await reopened.GetRequiredService<IToolExecutionAuditReader>().GetRecentAsync(taskId, 10, Token));
        Assert.Equal(ToolName, audit.ToolName);
        Assert.Equal(error, audit.ErrorCode);
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.NotNull(audit.CompletedAt);
        Assert.Equal(error is null, audit.IsSuccess);
        Assert.DoesNotContain("fixture-secret-marker", JsonSerializer.Serialize(history) + JsonSerializer.Serialize(audit));
        Assert.All(Model.Requests, request => Assert.DoesNotContain("fixture-secret-marker", request));
        Assert.Contains(history, message => message.Role == MessageRole.Assistant && message.Content == "MCP 链路验收已结束。");
        var visible = Assert.Single(Main.Workspace.Messages, message => message.ToolCallId == "mcp-flow");
        Assert.Equal(tool.ErrorCode, visible.Tool!.ErrorCode);
        Assert.Equal(tool.Details, visible.Tool.Details);
        if (error is null) Assert.Contains("重连后的真实 MCP 回复", tool.Details);
        Assert.False(Main.Approval.IsPending);
    }

    public async Task ReopenAsync()
    {
        var taskId = Main.Workspace.Catalog.SelectedTask!.Id;
        var theme = Window.RequestedThemeVariant;
        var size = Window.ClientSize;
        Window.Close();
        await _services!.DisposeAsync();
        _services = AppComposition.CreateServices(Path.Combine(_root, "data"));
        await Main.Workspace.LoadAsync();
        await WaitUntilAsync(() => Main.Workspace.Catalog.SelectedTask?.Id == taskId && Main.Workspace.Messages.Count > 0);
        Window = new MainWindow { DataContext = Main, Width = size.Width, Height = size.Height, RequestedThemeVariant = theme };
        Window.Show();
        ProviderUiWorkflowInput.Flush(Window);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_services is not null && Main.Workspace.IsRunning)
            {
                Main.Workspace.PrimaryActionCommand.Execute(null);
                await WaitUntilAsync(() => !Main.Workspace.IsRunning);
            }
            Window?.Close();
            if (_services is not null) await _services.DisposeAsync();
        }
        finally
        {
            if (_model is not null) await _model.DisposeAsync();
            if (_server is not null) await _server.DisposeAsync();
            await _timeout.CancelAsync();
            SqliteTestPool.Clear(Path.Combine(_root, "data"));
            var target = Path.GetFullPath(_root);
            var parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "MossAgent.Tests"));
            if (!target.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unexpected MCP recovery test root.");
            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            _timeout.Dispose();
        }
    }
}
