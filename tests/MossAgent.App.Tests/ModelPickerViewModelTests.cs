using Microsoft.Extensions.DependencyInjection;
using MossAgent.App.ViewModels;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

/// <summary>
/// 模型选择器视图模型单元测试，验证搜索过滤、分组展示及模型选中事件。
/// </summary>
public sealed class ModelPickerViewModelTests
{
    [Fact]
    public async Task ModelPicker_FilterAndSelect_WorksCorrectly()
    {
        await using var services = AppComposition.CreateServices();
        var workspace = services.GetRequiredService<WorkspaceViewModel>();
        var picker = workspace.ModelPicker;

        Assert.NotNull(picker);
        Assert.NotNull(picker.FilteredGroups);
        Assert.NotEmpty(picker.ReasoningOptions);
        Assert.Equal("默认", picker.SelectedReasoning);

        // 模拟选中
        var testProvider = new AiProvider(Guid.NewGuid(), "TestProvider", ProviderProtocol.OpenAiResponses, new Uri("https://api.openai.com/v1"), "key", null, true, true, new Dictionary<string, string>());
        var testModel = new ModelProfile(Guid.NewGuid(), testProvider.Id, "gpt-test", "GPT Test", 128000, 8192, "medium", null, null, true, true, true, true, "{}");

        AiProvider? selectedProvider = null;
        ModelProfile? selectedModel = null;
        picker.ModelSelected += (p, m) =>
        {
            selectedProvider = p;
            selectedModel = m;
        };

        var item = new ModelPickerItemViewModel(testProvider, testModel);
        picker.SelectItemCommand.Execute(item);

        Assert.Same(testProvider, selectedProvider);
        Assert.Same(testModel, selectedModel);
        Assert.Same(item, picker.SelectedItem);
        Assert.Contains("GPT Test", picker.CurrentDisplayName);
        Assert.Equal("medium", picker.EffectiveReasoningEffort);

        picker.SelectedReasoning = "高";
        Assert.Equal("high", picker.EffectiveReasoningEffort);
    }
}
