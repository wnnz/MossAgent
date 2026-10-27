using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.Agent;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class SubagentWorkspaceFlowTests
{
    [Theory]
    [InlineData(ApprovalPolicy.FullAccess, true, true)]
    [InlineData(ApprovalPolicy.FullAccess, true, false)]
    [InlineData(ApprovalPolicy.ReadOnly, false, true)]
    public async Task Run_PresentsPersistsAndAuditsSubagent(
        ApprovalPolicy policy, bool approved, bool shareContext)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestsTask = ServeResponsesAsync(listener, WireToolName(), approved, timeout.Token);

        try
        {
            await using var services = AppComposition.CreateServices(root);
            await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(timeout.Token);
            using var state = await CreateStateAsync(services, root, port, policy,
                shareContext, timeout.Token);
            var events = new List<AgentEvent>();
            var context = new ToolExecutionContext(state.Task.Id, root, [root], policy);

            var completed = await services.GetRequiredService<WorkspaceRunExecutor>()
                .ExecuteAsync(state, context, agentEvent =>
                {
                    events.Add(agentEvent);
                    state.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(
                        agentEvent, state.Assistant, state.Messages, _ => { }, _ => { });
                    if (agentEvent is AgentSubagentProgressEvent)
                    {
                        Assert.True(Assert.Single(state.Messages,
                            message => message.IsSubagentTool).IsToolRunning);
                    }
                    return Task.CompletedTask;
                }, timeout.Token);

            var requests = await requestsTask;
            AssertRequestRouting(requests, approved, shareContext);
            Assert.Equal(AgentTaskStatus.Completed, completed.Status);
            Assert.Equal("已汇总", state.Assistant.Content);

            AssertToolPresentation(events, state, approved);

            await AssertPersistenceAsync(services, state.Task, approved, timeout.Token);
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static void AssertRequestRouting(IReadOnlyList<string> requests, bool approved, bool shareContext)
    {
        Assert.Equal(approved ? ["parent-fixture", "child-fixture", "parent-fixture"]
            : ["parent-fixture", "parent-fixture"], requests.Select(ReadModelId));
        Assert.Contains("审查改动", ReadMessageContent(requests[0], "system"));
        if (!approved) return;
        using var childRequest = JsonDocument.Parse(requests[1]);
        Assert.Equal("high", childRequest.RootElement.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.Contains("检查代码。", ReadMessageContent(requests[1], "system"));
        var childPrompt = ReadMessageContent(requests[1], "user");
        Assert.Contains("检查改动", childPrompt);
        Assert.Equal(shareContext, childPrompt.Contains("共享对话上下文"));
    }

    private static void AssertToolPresentation(IEnumerable<AgentEvent> events, WorkspaceRunState state, bool approved)
    {
        var toolEvent = Assert.Single(events.OfType<AgentToolEvent>());
        Assert.Equal(SubagentTool.ToolName, toolEvent.ToolName);
        Assert.Equal(approved, toolEvent.Result.IsSuccess);
        Assert.Equal(approved, events.OfType<AgentSubagentProgressEvent>().Any());
        var card = Assert.Single(state.Messages, message => message.IsSubagentTool);
        Assert.False(card.IsToolRunning);
        Assert.Equal("call-sub", card.ToolCallId);
        Assert.Equal(approved ? "完成" : "失败", card.ToolStatus);
        Assert.True(state.Messages.IndexOf(card) < state.Messages.IndexOf(state.Assistant));
        if (!approved) return;
        Assert.Contains(state.Messages.Take(state.Messages.IndexOf(card)), message => message.Content == "准备审查。");
        Assert.Contains(card.Tool!.Dialogue!, message => message.Content == "检查完成");
    }

    private static async Task<WorkspaceRunState> CreateStateAsync(
        ServiceProvider services, string root, int port,
        ApprovalPolicy policy, bool shareContext, CancellationToken cancellationToken)
    {
        var configurations = services.GetRequiredService<IConfigurationRepository>();
        var provider = new AiProvider(Guid.NewGuid(), "fixture",
            ProviderProtocol.OpenAiResponses, new Uri($"http://127.0.0.1:{port}/v1/"),
            "fixture-key", null, true, true, new Dictionary<string, string>());
        var parentModel = CreateModel(provider.Id, "parent-fixture");
        var childModel = CreateModel(provider.Id, "child-fixture");
        await configurations.SaveProviderAsync(provider, cancellationToken);
        await configurations.SaveModelAsync(parentModel, cancellationToken);
        await configurations.SaveModelAsync(childModel, cancellationToken);
        var settings = services.GetRequiredService<SubagentSettingsViewModel>();
        await settings.RefreshCommand.ExecuteAsync(null);
        settings.NewCommand.Execute(null);
        settings.Name = "review";
        settings.TriggerCondition = "审查改动";
        settings.SystemPrompt = "检查代码。";
        settings.SelectedModel = Assert.Single(settings.Models,
            option => option.Model.Id == childModel.Id);
        settings.ReasoningEffort = "high";
        settings.ShareContext = shareContext;
        await settings.SaveCommand.ExecuteAsync(null);
        Assert.Contains("已保存", settings.Status);

        var workspaces = services.GetRequiredService<IWorkspaceRepository>();
        var project = new ProjectProfile(Guid.NewGuid(), "fixture", root,
            [root], DateTimeOffset.UtcNow);
        await workspaces.SaveProjectAsync(project, cancellationToken);
        const string prompt = "请审查改动";
        var task = await services.GetRequiredService<WorkspaceTaskFactory>()
            .CreateAsync(project, prompt, policy, false, cancellationToken);
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel>
        {
            new("你", prompt), assistant
        };
        return new WorkspaceRunState(task, project, provider, parentModel, messages, assistant);
    }

    private static ModelProfile CreateModel(Guid providerId, string modelId) =>
        new(Guid.NewGuid(), providerId, modelId, modelId,
            8192, 1024, "off", null, null, false, false,
            modelId == "parent-fixture", true, "{}");

    private static async Task AssertPersistenceAsync(
        ServiceProvider services, AgentTask task, bool approved, CancellationToken cancellationToken)
    {
        var workspaces = services.GetRequiredService<IWorkspaceRepository>();
        var persistedTask = Assert.Single(await workspaces.GetTasksAsync(
            task.ProjectId, cancellationToken), item => item.Id == task.Id);
        Assert.Equal(AgentTaskStatus.Completed, persistedTask.Status);
        var history = await workspaces.GetMessagesAsync(task.Id, cancellationToken);
        Assert.Equal(approved
                ? [MessageRole.User, MessageRole.Assistant, MessageRole.Tool, MessageRole.Assistant]
                : [MessageRole.User, MessageRole.Tool, MessageRole.Assistant],
            history.Select(message => message.Role));
        var toolMessage = Assert.Single(history, message => message.Role == MessageRole.Tool);
        Assert.Equal("call-sub", toolMessage.ToolCallId);
        var reloaded = WorkspaceConversationMapper.ToChatMessage(toolMessage);
        Assert.Equal("call-sub", reloaded.ToolCallId);
        Assert.Equal(approved, reloaded.Tool?.IsSuccess);
        if (approved)
        {
            Assert.True(reloaded.Tool!.HasDialogue);
            Assert.Contains(reloaded.Tool.Dialogue!, message => message.Content == "检查完成");
        }
        Assert.Contains(history, message => message.Role == MessageRole.Assistant
            && message.Content == "已汇总");

        var audit = Assert.Single(await services.GetRequiredService<IToolExecutionAuditReader>()
            .GetRecentAsync(task.Id, 10, cancellationToken),
            auditRecord => auditRecord.ToolName == SubagentTool.ToolName);
        Assert.Equal(approved, audit.ApprovalGranted);
        Assert.Equal(approved, audit.IsSuccess);
        if (!approved) Assert.Equal("capability_denied", audit.ErrorCode);
    }

    private static async Task<IReadOnlyList<string>> ServeResponsesAsync(
        TcpListener listener, string wireName, bool approved, CancellationToken cancellationToken)
    {
        var call = JsonSerializer.Serialize(new
        {
            type = "response.output_item.done",
            item = new
            {
                type = "function_call",
                call_id = "call-sub",
                name = wireName,
                arguments = JsonSerializer.Serialize(new { agent = "review", task = "检查改动" })
            }
        });
        var prelude = JsonSerializer.Serialize(new
        {
            type = "response.output_text.delta",
            delta = "准备审查。"
        });
        var responses = new List<string>
        {
            approved ? $"data: {prelude}\n\n" + Sse(call) : Sse(call)
        };
        if (approved) responses.Add(Sse(JsonSerializer.Serialize(new
        {
            type = "response.output_text.delta",
            delta = "检查完成"
        })));
        responses.Add(Sse(JsonSerializer.Serialize(new
        {
            type = "response.output_text.delta",
            delta = "已汇总"
        })));

        var requests = new List<string>();
        foreach (var response in responses)
        {
            using var client = await listener.AcceptTcpClientAsync(cancellationToken);
            await using var stream = client.GetStream();
            requests.Add(await ReadRequestBodyAsync(stream, cancellationToken));
            var bytes = Encoding.UTF8.GetBytes(response);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(bytes, cancellationToken);
        }
        return requests;
    }

    private static async Task<string> ReadRequestBodyAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        using var received = new MemoryStream();
        var chunk = new byte[4096];
        var bodyStart = -1;
        var bodyLength = 0;
        while (received.Length < 1_000_000)
        {
            var count = await stream.ReadAsync(chunk, cancellationToken);
            if (count == 0) throw new EndOfStreamException();
            received.Write(chunk, 0, count);
            var bytes = received.ToArray();
            if (bodyStart < 0 && bytes.AsSpan().IndexOf("\r\n\r\n"u8) is var headerEnd and >= 0)
            {
                bodyStart = headerEnd + 4;
                var header = Encoding.ASCII.GetString(bytes, 0, bodyStart);
                var length = header.Split("\r\n").First(line =>
                    line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase));
                bodyLength = int.Parse(length.Split(':', 2)[1].Trim());
            }
            if (bodyStart >= 0 && bytes.Length >= bodyStart + bodyLength)
                return Encoding.UTF8.GetString(bytes, bodyStart, bodyLength);
        }
        throw new InvalidDataException("Model request exceeded test limit.");
    }

    private static string ReadModelId(string request)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement.GetProperty("model").GetString() ?? string.Empty;
    }

    private static string ReadMessageContent(string request, string role)
    {
        using var document = JsonDocument.Parse(request);
        return document.RootElement.GetProperty("input").EnumerateArray()
            .First(message => message.TryGetProperty("role", out var value)
                && value.GetString() == role)
            .GetProperty("content").GetString() ?? string.Empty;
    }

    private static string WireToolName() => "subagent_run__"
        + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SubagentTool.ToolName)))
            [..12].ToLowerInvariant();

    private static string Sse(string data) => $"data: {data}\n\n"
        + "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"fixture-response\",\"status\":\"completed\"}}\n\n"
        + "data: [DONE]\n\n";
}
