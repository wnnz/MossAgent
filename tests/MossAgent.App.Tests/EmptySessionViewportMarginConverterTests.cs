using System.Globalization;
using Avalonia;
using MossAgent.App.Controls;
using MossAgent.Domain;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class EmptySessionViewportMarginConverterTests
{
    [Fact]
    public void ActiveTask_UsesMeasuredCardInset()
    {
        var converter = new EmptySessionViewportMarginConverter();
        var task = new AgentTask(Guid.NewGuid(), Guid.NewGuid(), "Task", ApprovalPolicy.FullAccess,
            AgentTaskStatus.Completed, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null);
        var inset = new Thickness(0, 218, 0, 0);

        Assert.Equal(inset, converter.Convert([task, inset], typeof(Thickness), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DraftOrUnavailableCard_HasNoInset()
    {
        var converter = new EmptySessionViewportMarginConverter();
        var inset = new Thickness(0, 218, 0, 0);

        Assert.Equal(new Thickness(0), converter.Convert([null, inset], typeof(Thickness), null, CultureInfo.InvariantCulture));
        Assert.Equal(new Thickness(0), converter.Convert([new object()], typeof(Thickness), null, CultureInfo.InvariantCulture));
    }
}
