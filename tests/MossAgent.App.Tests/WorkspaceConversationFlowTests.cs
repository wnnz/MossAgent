using System.Collections.ObjectModel;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Application.Agent;
using MossAgent.Application.Persistence;
using MossAgent.Domain;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class WorkspaceConversationFlowTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfiguredModel_StreamsAndPersistsConversation(bool fail)
    {
        var root = Path.Combine(Path.GetTempPath(), "MossAgent.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requestTask = ServeAsync(listener, fail, timeout.Token);

        try
        {
            var (projectId, taskId) = await RunAndAssertAsync(root, port, fail, timeout.Token);
            AssertRequest(await requestTask);
            await AssertPersistedAsync(root, projectId, taskId, fail, timeout.Token);
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
            SqliteTestPool.Clear(root);
            Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<(Guid ProjectId, Guid TaskId)> RunAndAssertAsync(
        string root, int port, bool fail, CancellationToken cancellationToken)
    {
        await using var services = AppComposition.CreateServices(root);
        await services.GetRequiredService<IDatabaseInitializer>().InitializeAsync(cancellationToken);
        var provider = new AiProvider(Guid.NewGuid(), "flow-fixture",
            ProviderProtocol.OpenAiResponses, new Uri($"http://127.0.0.1:{port}/v1/"),
            "fixture-key", null, true, true, new Dictionary<string, string>());
        var model = new ModelProfile(Guid.NewGuid(), provider.Id,
            "flow-fixture-model", "Flow Fixture", 8192, 1024, "off",
            null, null, false, false, true, true, "{}");
        var configurations = services.GetRequiredService<IConfigurationRepository>();
        await configurations.SaveProviderAsync(provider, cancellationToken);
        await configurations.SaveModelAsync(model, cancellationToken);
        var project = await services.GetRequiredService<WorkspaceProjectService>()
            .CreateAsync("Runtime Flow", [root]);

        var workspace = services.GetRequiredService<WorkspaceViewModel>();
        await workspace.LoadAsync();
        Assert.Equal(project.Id, workspace.Catalog.SelectedProject?.Id);
        Assert.Equal(provider.Id, workspace.SelectedProvider?.Id);
        Assert.Equal(model.Id, workspace.SelectedModel?.Id);

        const string prompt = "Reply using the local fixture.";
        var task = await services.GetRequiredService<WorkspaceTaskFactory>()
            .CreateAsync(project, prompt, ApprovalPolicy.ReadOnly, false, cancellationToken);
        var assistant = new ChatMessageViewModel("MossAgent", string.Empty);
        var messages = new ObservableCollection<ChatMessageViewModel>
        {
            new("你", prompt), assistant
        };
        using var state = new WorkspaceRunState(
            task, project, workspace.SelectedProvider!, workspace.SelectedModel!, messages, assistant);
        var context = new ToolExecutionContext(task.Id, root, [root], ApprovalPolicy.ReadOnly);
        var events = new List<AgentEvent>();
        var firstDeltaVisible = false;
        var activity = string.Empty;
        var completed = await services.GetRequiredService<WorkspaceRunExecutor>()
            .ExecuteAsync(state, context, agentEvent =>
            {
                events.Add(agentEvent);
                state.Assistant = WorkspaceAgentEventPresenter.ApplyEvent(agentEvent, state.Assistant, messages,
                    status => activity = status, _ => { });
                if (agentEvent is AgentTextEvent { Delta: "本地流式" })
                    firstDeltaVisible = assistant.Content == "本地流式";
                return Task.CompletedTask;
            }, cancellationToken);

        Assert.Equal(fail ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            completed.Status);
        Assert.Equal(fail ? string.Empty : "本地流式测试已完成。", assistant.Content);
        Assert.Equal(!fail, firstDeltaVisible);
        Assert.Equal(!fail, events.OfType<AgentCompletedEvent>().Any());
        Assert.Equal(fail, events.OfType<AgentFailureEvent>().Any());
        Assert.Equal(fail ? WorkspaceFailureText.Get(TaskFailureReason.ProviderHttp) : WorkspaceText.Get("RunCompleted"), activity);
        Assert.Equal(fail ? TaskFailureReason.ProviderHttp : (TaskFailureReason?)null, completed.FailureReason);
        return (project.Id, task.Id);
    }

    private static void AssertRequest(string request)
    {
        using var document = JsonDocument.Parse(request);
        Assert.Equal("flow-fixture-model",
            document.RootElement.GetProperty("model").GetString());
        Assert.Contains("Reply using the local fixture.", request);
    }

    private static async Task AssertPersistedAsync(
        string root, Guid projectId, Guid taskId, bool fail,
        CancellationToken cancellationToken)
    {
        await using var reopened = AppComposition.CreateServices(root);
        var persisted = reopened.GetRequiredService<IWorkspaceRepository>();
        var savedTask = Assert.Single(await persisted.GetTasksAsync(projectId, cancellationToken));
        Assert.Equal(taskId, savedTask.Id);
        Assert.Equal(fail ? AgentTaskStatus.Failed : AgentTaskStatus.Completed,
            savedTask.Status);
        Assert.Equal(fail ? TaskFailureReason.ProviderHttp : (TaskFailureReason?)null, savedTask.FailureReason);
        var history = await persisted.GetMessagesAsync(taskId, cancellationToken);
        Assert.Contains(history, message => message.Role == MessageRole.User);
        Assert.Equal(!fail, history.Any(message => message.Role == MessageRole.Assistant
            && message.Content == "本地流式测试已完成。"));
    }

    private static async Task<string> ServeAsync(
        TcpListener listener, bool fail, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        await using var stream = client.GetStream();
        var request = await ReadRequestBodyAsync(stream, cancellationToken);
        if (fail)
        {
            var body = Encoding.UTF8.GetBytes("{\"error\":{\"message\":\"synthetic unavailable\"}}");
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 503 Service Unavailable\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(body, cancellationToken);
            return request;
        }

        await stream.WriteAsync(Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\nContent-Type: text/event-stream\r\nConnection: close\r\n\r\n"),
            cancellationToken);
        foreach (var data in new[]
        {
            new { type = "response.output_text.delta", delta = "本地流式" },
            new { type = "response.output_text.delta", delta = "测试已完成。" }
        })
        {
            await stream.WriteAsync(Encoding.UTF8.GetBytes(
                $"data: {JsonSerializer.Serialize(data)}\n\n"), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            await Task.Delay(40, cancellationToken);
        }
        await stream.WriteAsync(Encoding.UTF8.GetBytes(
            "data: {\"type\":\"response.completed\",\"response\":{\"id\":\"fixture\"}}\n\ndata: [DONE]\n\n"),
            cancellationToken);
        return request;
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
}
