using System.Xml.Linq;
using System.Xml.XPath;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.Input;
using MossAgent.App.Controls;
using MossAgent.App.ViewModels;
using Xunit;

namespace MossAgent.App.Tests;

[Collection("Avalonia visual controls")]
public sealed class ToolActivityPresentationTests
{
    [Theory]
    [InlineData("读取完成\nSHA-256: fixture", "读取完成", true)]
    [InlineData("读取完成\r\nSHA-256: fixture", "读取完成", true)]
    [InlineData("读取完成\rSHA-256: fixture", "读取完成", true)]
    [InlineData("读取完成", "读取完成", false)]
    [InlineData("读取完成\n", "读取完成", false)]
    [InlineData("读取完成\r\n \t\u2003", "读取完成", false)]
    [InlineData("读取完成\n元数据 \t\u2003", "读取完成", true)]
    [InlineData("读取完成\u2028元数据", "读取完成\u2028元数据", false)]
    public void Header_IsCompactWithoutDiscardingMetadata(string summary, string header, bool hasMetadata)
    {
        var message = ChatMessageViewModel.CreateTool(new ToolConversationPayload("test.read", true, summary, null));

        Assert.Equal(header, message.ToolSummary);
        Assert.Equal(summary, message.Tool!.Summary);
        Assert.Equal(hasMetadata, message.HasToolSummaryDetails);
        Assert.Equal(hasMetadata ? summary : string.Empty, message.ToolSummaryDetails);
        Assert.Equal(hasMetadata, message.ToggleToolCommand.CanExecute(null));
        if (hasMetadata)
        {
            message.ToggleToolCommand.Execute(null);
            Assert.True(message.IsToolExpanded);
        }
    }

    [Fact]
    public void LiveResult_NotifiesMetadataAndKeepsRawOutput()
    {
        var message = ChatMessageViewModel.CreateTool(new ToolConversationPayload("test.read", false, "读取中", null),
            isRunning: true);
        var changes = new List<string?>();
        message.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        message.UpdateTool(new ToolConversationPayload("test.read", true, "读取完成\nSHA-256: fixture", "alpha\n"), false);

        Assert.Equal("读取完成", message.ToolSummary);
        Assert.Equal("alpha\n", message.Tool!.Details);
        Assert.Contains(nameof(ChatMessageViewModel.ToolSummary), changes);
        Assert.Contains(nameof(ChatMessageViewModel.ToolSummaryDetails), changes);
        Assert.Contains(nameof(ChatMessageViewModel.HasToolSummaryDetails), changes);
        Assert.Contains(nameof(ChatMessageViewModel.HasToolDetails), changes);
        Assert.Equal("完成", message.ToolStatus);
    }

    [Fact]
    public void MetadataAvailability_DoesNotCopyTrailingWhitespace()
    {
        var message = ChatMessageViewModel.CreateTool(new("test.read", true, "完成\n元数据" + new string(' ', 2_000), null));
        for (var index = 0; index < 128; index++) Assert.True(message.HasToolDetails);
        var available = true;
        var start = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++) available &= message.HasToolDetails;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.True(available);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void SharedRow_ExposesSemanticStateWithoutExecutingTheCommand()
    {
        var invoked = 0;
        var row = new AppToolActivityRow
        {
            Summary = "读取 fixture.txt",
            Status = "完成",
            Description = "读取 fixture.txt\nSHA-256: fixture",
            CanExpand = true,
            Command = new RelayCommand(() => invoked++)
        };
        Assert.IsType<AppButton>(row.FindControl<AppButton>("ToolButton"));
        Assert.IsType<AppBorder>(row.FindControl<AppBorder>("ToolStaticSurface"));
        Assert.Equal("读取 fixture.txt", row.Summary);
        Assert.Equal("完成", row.Status);
        Assert.True(row.CanExpand);
        Assert.Equal(0, invoked);
        row.Command!.Execute(null);
        Assert.Equal(1, invoked);

        row.CanExpand = false;
        row.Summary = "操作已结束";

        Assert.False(row.CanExpand);
        Assert.Equal("操作已结束", row.Summary);
    }

    [Fact]
    public void Markup_UsesSharedRowAndKeepsSummaryInExpandedDetails()
    {
        var root = AppBuildDefinition.RepositoryRoot;
        var view = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Views", "WorkspaceMessageView.axaml"));
        var row = Assert.Single(view.Descendants(), element => element.Name.LocalName == "AppToolActivityRow");
        Assert.Equal("{Binding ToolSummary}", row.Attribute("Summary")?.Value);
        Assert.Equal("{Binding HasToolDetails}", row.Attribute("CanExpand")?.Value);
        Assert.Contains(view.Descendants(), element => element.Attribute("Text")?.Value == "{Binding ToolSummaryDetails}"
            && element.Attribute("IsVisible")?.Value == "{Binding HasToolSummaryDetails}");

        var component = XDocument.Load(Path.Combine(root, "src", "MossAgent.App", "Controls", "AppToolActivityRow.axaml"));
        Assert.All(component.Descendants().Where(element => element.Name.LocalName is "AppText" or "Panel"),
            element => Assert.Equal("Center", element.Attribute("VerticalAlignment")?.Value));
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "AppText"
            && element.Attribute("TextTrimming")?.Value == "CharacterEllipsis");
        Assert.All(component.Descendants().Where(element => element.Name.LocalName == "AppContentControl"),
            element => Assert.Equal("{Binding Summary}", element.Attribute("Content")?.Value));
        var button = Assert.Single(component.Descendants(), element => element.Name.LocalName == "AppButton");
        Assert.Equal("{Binding Summary}", button.Attribute("AutomationProperties.Name")?.Value);
        Assert.Equal("{Binding Status}", button.Attribute("AutomationProperties.HelpText")?.Value);
        Assert.Equal("{Binding CanExpand}", button.Attribute("IsVisible")?.Value);
        Assert.Equal("{Binding Command}", button.Attribute("Command")?.Value);
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "AppBorder"
            && element.Attribute("IsVisible")?.Value == "{Binding !CanExpand}");
        Assert.Contains(component.Descendants(), element => element.Name.LocalName == "Panel"
            && element.Attribute("IsVisible")?.Value == "{Binding CanExpand}");
    }

    [Fact]
    public void BuildGuard_RejectsToolRowCompositionAndSizingOverrides()
    {
        var project = AppBuildDefinition.Load();
        var target = Assert.Single(project.Descendants(), element => element.Name.LocalName == "Target"
            && element.Attribute("Name")?.Value == "ValidateSharedToolActivityRows");
        Assert.Equal("CoreCompile", target.Attribute("BeforeTargets")?.Value);
        var query = target.Elements().Single(element => element.Name.LocalName == "XmlPeek").Attribute("Query")!.Value;
        var sample = XDocument.Parse("""
            <UserControl xmlns="https://github.com/avaloniaui" xmlns:ui="clr-namespace:MossAgent.App.Controls">
              <ui:AppToolActivityRow Content="override" />
              <ui:AppToolActivityRow Height="60" />
              <ui:AppToolActivityRow><UserControl.Content><ui:AppText /></UserControl.Content></ui:AppToolActivityRow>
              <ui:AppToolActivityRow><ui:AppToolActivityRow.MaxWidth>100</ui:AppToolActivityRow.MaxWidth></ui:AppToolActivityRow>
              <ui:AppToolActivityRow Summary="allowed" CanExpand="True" Command="{Binding Toggle}" />
            </UserControl>
            """);

        Assert.Equal(4, sample.XPathSelectElements(query).Count());
    }

}
