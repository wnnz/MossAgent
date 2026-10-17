using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using MossAgent.App.Controls;
using MossAgent.Application.Models;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ProviderUiWorkflowTests : IAsyncLifetime
{
    private readonly HeadlessUnitTestSession _session = HeadlessUnitTestSession.StartNew(typeof(SharedVisualTestApplication));

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;
    public ValueTask DisposeAsync() => _session.DisposeAsync();

    [Theory]
    [InlineData(ProviderProtocol.OpenAiResponses, false)]
    [InlineData(ProviderProtocol.OpenAiResponses, true)]
    [InlineData(ProviderProtocol.OpenAiCompatible, false)]
    [InlineData(ProviderProtocol.OpenAiCompatible, true)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, false)]
    [InlineData(ProviderProtocol.GeminiGenerateContent, true)]
    public Task VisibleSettingsRecoverDiscoverSaveAndRunWithoutCrossProviderMutation(ProviderProtocol protocol, bool dark) =>
        _session.DispatchAsync(async () =>
        {
            await using var fixture = await ProviderUiWorkflowFixture.CreateAsync(protocol, dark);
            var provider = await CreateProviderAsync(fixture, protocol);
            await ProviderUiWorkflowInput.FetchAsync(fixture, provider.Id);
            Assert.True(fixture.Main.Models.HasStatusError);
            Assert.Contains("503", fixture.Main.Models.Status);
            Assert.DoesNotContain("synthetic-private-provider-key", fixture.Main.Models.Status);
            Assert.Empty(await fixture.Configurations.GetModelsAsync(provider.Id, fixture.Token));
            var status = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppSettingsPageHeader>());
            Assert.True(status.IsStatusError);
            Assert.Equal(fixture.Main.Models.Status, status.Status);
            await ProviderUiWorkflowInput.FetchAsync(fixture, provider.Id);
            Assert.False(fixture.Main.Models.HasStatusError, fixture.Main.Models.Status);
            Assert.False(status.IsStatusError);
            var imported = Assert.Single(await fixture.Configurations.GetModelsAsync(provider.Id, fixture.Token));
            Assert.Equal(fixture.Server.ModelId, imported.ModelId);
            var capabilities = Assert.IsType<ModelCapabilities>(ModelCapabilityCatalog.Find(protocol, imported.ModelId));
            Assert.Equal(capabilities.SupportsImages, imported.SupportsImages);
            Assert.Equal(capabilities.SupportsFiles, imported.SupportsFiles);
            var saved = await EditAndRetryAsync(fixture, imported);
            await ProviderUiWorkflowInput.FetchAsync(fixture, provider.Id);
            Assert.Equal(saved, Assert.Single(await fixture.Configurations.GetModelsAsync(provider.Id, fixture.Token)));
            await ProviderUiWorkflowInput.SetProviderEnabledAsync(fixture, provider.Id, false);
            fixture.Main.CloseSettingsCommand.Execute(null);
            TypeComposer(fixture);
            Assert.False(ProviderUiWorkflowInput.SendButton(fixture.Window).IsEffectivelyEnabled);
            Submit(fixture);
            Assert.Empty(fixture.Server.Requests);
            Assert.False(fixture.Main.Workspace.IsRunning);
            Assert.Empty(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
            fixture.Main.SelectSettingsTabCommand.Execute("0");
            ProviderUiWorkflowInput.Flush(fixture.Window);
            await ProviderUiWorkflowInput.SetProviderEnabledAsync(fixture, provider.Id, true);
            fixture.Main.CloseSettingsCommand.Execute(null);
            ProviderUiWorkflowInput.Flush(fixture.Window);
            Assert.Equal(saved.Id, fixture.Main.Workspace.SelectedModel?.Id);
            Assert.Equal("high", fixture.Main.Workspace.ModelPicker.EffectiveReasoningEffort);
            Assert.True(ProviderUiWorkflowInput.SendButton(fixture.Window).IsEffectivelyEnabled);
            Submit(fixture);
            await fixture.WaitUntilAsync(() => fixture.Server.Requests.Count == 1 && !fixture.Main.Workspace.IsRunning);
            AssertRequest(Assert.Single(fixture.Server.Requests), protocol);
            var task = Assert.Single(await fixture.Tasks.GetTasksAsync(null, fixture.Token));
            Assert.Null(task.ProjectId);
            Assert.Equal(AgentTaskStatus.Completed, task.Status);
            var messages = await fixture.Tasks.GetMessagesAsync(task.Id, fixture.Token);
            Assert.Equal("界面连通验收", Assert.Single(messages, message => message.Role == MessageRole.User).Content);
            Assert.Contains(messages, message => message.Role == MessageRole.Assistant && message.Content.Contains("hello", StringComparison.Ordinal));
            ProviderUiWorkflowInput.AssertAnswerVisible(fixture.Window);
            Assert.Equal(3, fixture.Server.Discoveries);
            var streamPath = protocol switch
            {
                ProviderProtocol.OpenAiResponses => "/v1/responses",
                ProviderProtocol.OpenAiCompatible => "/v1/chat/completions",
                _ => $"/v1/models/{fixture.Server.ModelId}:streamGenerateContent"
            };
            Assert.Equal(["/v1/models", "/v1/models", "/v1/models", streamPath], fixture.Server.Paths);
            await fixture.AssertUntouchedAsync();
        }, TestContext.Current.CancellationToken);

    private static async Task<AiProvider> CreateProviderAsync(ProviderUiWorkflowFixture fixture, ProviderProtocol protocol)
    {
        var window = fixture.Window;
        var create = Assert.Single(window.GetVisualDescendants().OfType<AppActionButton>(),
            button => button.IsEffectivelyVisible && button.Label == "添加提供商");
        ProviderUiWorkflowInput.Press(window, create);
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "提供商名称"), "Workflow provider");
        ProviderUiWorkflowInput.Named<AppComboBox>(window, "协议类型").SelectedItem =
            Assert.Single(fixture.Main.Providers.ProtocolOptions, option => option.Protocol == protocol);
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "Base URL"), fixture.Server.BaseUri.AbsoluteUri);
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "API Key"), "synthetic-fixture-key");
        ProviderUiWorkflowInput.Press(window, ProviderUiWorkflowInput.Button(window, "保存提供商"));
        await fixture.Main.Providers.SaveCommand.ExecutionTask!.WaitAsync(fixture.Token);
        ProviderUiWorkflowInput.Flush(window);
        Assert.False(fixture.Main.Providers.IsFormOpen);
        var provider = Assert.Single(await fixture.Configurations.GetProvidersAsync(fixture.Token), item => item.Id != fixture.UntouchedProvider.Id);
        Assert.Equal(protocol, provider.Protocol);
        Assert.Null(provider.ProxyId);
        return provider;
    }

    private static async Task<ModelProfile> EditAndRetryAsync(ProviderUiWorkflowFixture fixture, ModelProfile imported)
    {
        var window = fixture.Window;
        ProviderUiWorkflowInput.Edit(fixture, imported.Id);
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "模型 ID"), string.Empty);
        await ProviderUiWorkflowInput.SaveModelAsync(fixture);
        Assert.True(fixture.Main.Models.IsFormOpen);
        Assert.True(fixture.Main.Models.HasEditorStatus);
        Assert.Equal(imported, Assert.Single(await fixture.Configurations.GetModelsAsync(imported.ProviderId, fixture.Token)));
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "模型 ID"), imported.ModelId);
        ProviderUiWorkflowInput.Type(window, ProviderUiWorkflowInput.Named<AppInput>(window, "显示名称"), "Edited workflow model");
        ProviderUiWorkflowInput.Named<AppNumericUpDown>(window, "上下文长度").Value = 64000;
        ProviderUiWorkflowInput.Named<AppNumericUpDown>(window, "最大输出 Token").Value = 2048;
        ProviderUiWorkflowInput.Named<AppNumericUpDown>(window, "Temperature").Value = 0.3m;
        ProviderUiWorkflowInput.Named<AppNumericUpDown>(window, "Top-P").Value = 0.8m;
        var choice = Assert.Single(window.GetVisualDescendants().OfType<AppEditableChoice>(), control => control.IsEffectivelyVisible);
        ProviderUiWorkflowInput.Type(window, choice.FindControl<AppInput>("ChoiceInput")!, "high");
        ProviderUiWorkflowInput.Check(window, "支持图像", false);
        ProviderUiWorkflowInput.Check(window, "支持文件", true);
        await ProviderUiWorkflowInput.SaveModelAsync(fixture);
        Assert.False(fixture.Main.Models.IsFormOpen);
        var saved = Assert.Single(await fixture.Configurations.GetModelsAsync(imported.ProviderId, fixture.Token));
        Assert.Equal(64000, saved.ContextLength);
        Assert.Equal(2048, saved.MaximumOutputTokens);
        Assert.Equal("high", saved.ReasoningEffort);
        Assert.Equal(0.3, saved.Temperature);
        Assert.Equal(0.8, saved.TopP);
        Assert.False(saved.SupportsImages);
        Assert.True(saved.SupportsFiles);
        ProviderUiWorkflowInput.Edit(fixture, saved.Id);
        Assert.Equal("Edited workflow model", ProviderUiWorkflowInput.Named<AppInput>(window, "显示名称").Text);
        window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        window.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
        ProviderUiWorkflowInput.Flush(window);
        Assert.False(fixture.Main.Models.IsFormOpen);
        return saved;
    }

    private static void TypeComposer(ProviderUiWorkflowFixture fixture)
    {
        ProviderUiWorkflowInput.Flush(fixture.Window);
        var input = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppInput>(), control => control.Classes.Contains("composer-input"));
        ProviderUiWorkflowInput.Type(fixture.Window, input, "界面连通验收");
    }

    private static void Submit(ProviderUiWorkflowFixture fixture)
    {
        var input = Assert.Single(fixture.Window.GetVisualDescendants().OfType<AppInput>(), control => control.Classes.Contains("composer-input"));
        Assert.True(input.Focus());
        fixture.Window.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        fixture.Window.KeyRelease(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "\r");
        ProviderUiWorkflowInput.Flush(fixture.Window);
    }

    private static void AssertRequest(JsonElement request, ProviderProtocol protocol)
    {
        if (protocol == ProviderProtocol.GeminiGenerateContent)
        {
            var config = request.GetProperty("generationConfig");
            Assert.Equal(2048, config.GetProperty("maxOutputTokens").GetInt32());
            Assert.Equal("HIGH", config.GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
            Assert.Equal(0.3, config.GetProperty("temperature").GetDouble());
            Assert.Equal(0.8, config.GetProperty("topP").GetDouble());
            Assert.All(request.GetProperty("tools").EnumerateArray(), tool =>
                Assert.Empty(tool.GetProperty("functionDeclarations").EnumerateArray()));
        }
        else
        {
            Assert.Equal("gpt-5", request.GetProperty("model").GetString());
            Assert.Equal(2048, request.GetProperty(protocol == ProviderProtocol.OpenAiResponses ? "max_output_tokens" : "max_tokens").GetInt32());
            Assert.Equal("high", protocol == ProviderProtocol.OpenAiResponses
                ? request.GetProperty("reasoning").GetProperty("effort").GetString() : request.GetProperty("reasoning_effort").GetString());
            Assert.Equal(0.3, request.GetProperty("temperature").GetDouble());
            Assert.Equal(0.8, request.GetProperty("top_p").GetDouble());
            Assert.False(request.TryGetProperty("tools", out var tools) && tools.GetArrayLength() > 0);
        }
    }
}
