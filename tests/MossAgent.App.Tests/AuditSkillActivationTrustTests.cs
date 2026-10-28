using MossAgent.App.Resources;
using MossAgent.App.ViewModels;
using MossAgent.Domain.Plugins;
using Xunit;

namespace MossAgent.App.Tests;

public sealed class AuditSkillActivationTrustTests
{
    private static TaskSkillActivation Record(long trustRevision) => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), "demo-plugin", "1.2.0", new string('a', 64), "skills/a/SKILL.md",
        "bounded skill body", DateTimeOffset.UnixEpoch, trustRevision);

    [Fact]
    public void MetadataShowsTheRecordedRevisionWithoutAPluginLookup()
    {
        var item = new AuditSkillActivationViewModel(Record(3));
        Assert.Contains("· r3", item.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
        Assert.DoesNotContain("信任已变化", item.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingPluginRowShowsTheRemovedMarker()
    {
        var item = new AuditSkillActivationViewModel(Record(2), currentTrustRevision: null,
            pluginChecked: true);
        Assert.Contains(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DifferingCurrentRevisionShowsTheTrustChangedMarker()
    {
        var item = new AuditSkillActivationViewModel(Record(2), currentTrustRevision: 4,
            pluginChecked: true);
        Assert.Contains(InspectorText.Format("AuditSkillTrustChanged", 2, 4), item.Metadata,
            StringComparison.Ordinal);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
    }

    [Fact]
    public void MatchingCurrentRevisionShowsNoMarker()
    {
        var item = new AuditSkillActivationViewModel(Record(2), currentTrustRevision: 2,
            pluginChecked: true);
        Assert.Contains("· r2", item.Metadata, StringComparison.Ordinal);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
        Assert.DoesNotContain("信任已变化", item.Metadata, StringComparison.Ordinal);
    }

    [Fact]
    public void UncheckedPluginDoesNotReadAsRemoved()
    {
        // Without a plugin store the pane cannot distinguish absent from unlooked-up, so no
        // removal marker is shown — only the recorded revision.
        var item = new AuditSkillActivationViewModel(Record(2), currentTrustRevision: null,
            pluginChecked: false);
        Assert.DoesNotContain(InspectorText.Get("AuditSkillPluginRemoved"), item.Metadata,
            StringComparison.Ordinal);
    }
}
