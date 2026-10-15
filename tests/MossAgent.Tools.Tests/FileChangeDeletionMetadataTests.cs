using System.Text.Json;
using MossAgent.Tools.Abstractions;
using Xunit;

namespace MossAgent.Tools.Tests;

public sealed class FileChangeDeletionMetadataTests
{
    [Fact]
    public void ExistingRecords_KeepTheirJsonShapeAndMeaning()
    {
        var change = new ToolFileChange("file.cs", null, new string('A', 64));
        var json = JsonSerializer.Serialize(change);

        Assert.DoesNotContain("AfterDeleted", json);
        var restored = JsonSerializer.Deserialize<ToolFileChange>(json);
        Assert.Equal(change, restored);
        Assert.True(restored!.IsValid);
    }

    [Fact]
    public void ADeletion_RetainsItsExplicitMarkerAcrossSerialization()
    {
        var change = new ToolFileChange("file.cs", new string('A', 64), null, AfterDeleted: true);
        var json = JsonSerializer.Serialize(change);

        Assert.Contains("\"AfterDeleted\":true", json);
        var restored = JsonSerializer.Deserialize<ToolFileChange>(json);
        Assert.Equal(change, restored);
        Assert.True(restored!.IsValid);
        Assert.Null(restored.AfterSha256);
    }

    [Fact]
    public void LegacyInvalidNullAfter_IsNotSilentlyReinterpretedAsADeletion()
    {
        var json = JsonSerializer.Serialize(new { Path = "file.cs", BeforeSha256 = new string('A', 64), AfterSha256 = (string?)null });

        var restored = JsonSerializer.Deserialize<ToolFileChange>(json);

        Assert.False(restored!.IsValid);
        Assert.False(restored.AfterDeleted);
    }

    [Fact]
    public void EmptyHistoryOrContradictoryMarkers_AreStillInvalid()
    {
        Assert.False(new ToolFileChange("file.cs", null, null, AfterDeleted: true).IsValid);
        Assert.False(new ToolFileChange("file.cs", new string('A', 64), new string('B', 64), AfterDeleted: true).IsValid);
    }
}
