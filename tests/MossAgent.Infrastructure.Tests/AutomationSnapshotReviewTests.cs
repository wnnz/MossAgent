using MossAgent.Domain;
using Xunit;

namespace MossAgent.Infrastructure.Tests;

public sealed class AutomationSnapshotReviewTests
{
    private static readonly ProjectProfile Project = new(Guid.NewGuid(), "project", "/root",
        ["/root", "/shared"], DateTimeOffset.UtcNow);
    private static readonly AiProvider Provider = new(Guid.NewGuid(), "provider",
        ProviderProtocol.OpenAiResponses, new Uri("http://localhost"), "key", null, false, true,
        new Dictionary<string, string>());
    private static readonly ModelProfile Model = new(Guid.NewGuid(), Provider.Id, "model", "Model",
        32000, 1024, "medium", null, null, false, false, false, true, "{}");

    private static AutomationAuthorizationSnapshot Snapshot(IReadOnlyList<string>? directories = null) =>
        new(1, Project.Id, Project.AuthorizationRevision, directories ?? Project.AuthorizedDirectories,
            Provider.Id, Provider.Name, Model.Id, "Model", ApprovalPolicy.ReadOnly, "prompt",
            DateTimeOffset.UtcNow);

    private static AutomationSchedule Plan(AutomationAuthorizationSnapshot? snapshot, bool follow = false) =>
        new(Guid.NewGuid(), "plan", "prompt", Project.Id, Provider.Id, Model.Id,
            AutomationFrequency.Daily, 1, 9, 0, DayOfWeek.Monday, "UTC", ApprovalPolicy.ReadOnly,
            30, false, DateTimeOffset.UtcNow.AddDays(1), FollowAuthorizationChanges: follow)
        { AuthorizationSnapshot = snapshot };

    private static ProjectProfile Drifted(string drift) => drift switch
    {
        "revision" => Project with { AuthorizationRevision = Project.AuthorizationRevision + 1 },
        "expanded" => Project with
        {
            AuthorizationRevision = Project.AuthorizationRevision + 1,
            AuthorizedDirectories = [.. Project.AuthorizedDirectories, "/new-root"]
        },
        _ => Project with
        {
            AuthorizationRevision = Project.AuthorizationRevision + 1,
            AuthorizedDirectories = [Project.PrimaryDirectory]
        }
    };

    [Fact]
    public void PinnedPlan_PassesWhenLiveStateMatchesTheSnapshot()
    {
        Assert.Null(AutomationSnapshotReview.Evaluate(Plan(Snapshot()), Project, Provider, Model));
    }

    [Theory]
    [InlineData("revision", AutomationSnapshotReview.RevisionChanged)]
    [InlineData("expanded", AutomationSnapshotReview.RevisionChanged)]
    [InlineData("reduced", AutomationSnapshotReview.RevisionChanged)]
    public void PinnedPlan_BlocksAuthorizationDrift(string drift, string expected)
    {
        Assert.Equal(expected, AutomationSnapshotReview.Evaluate(
            Plan(Snapshot()), Drifted(drift), Provider, Model));
    }

    [Fact]
    public void PinnedPlan_BlocksSnapshotDirectoriesOutsideLiveSet()
    {
        var project = Project with { AuthorizedDirectories = [Project.PrimaryDirectory] };
        Assert.Equal(AutomationSnapshotReview.DirectoriesReduced,
            AutomationSnapshotReview.Evaluate(Plan(Snapshot()), project, Provider, Model));
    }

    [Theory]
    [InlineData("revision")]
    [InlineData("expanded")]
    [InlineData("reduced")]
    public void FollowPlan_IgnoresAuthorizationDrift(string drift)
    {
        Assert.Null(AutomationSnapshotReview.Evaluate(
            Plan(Snapshot(), follow: true), Drifted(drift), Provider, Model));
    }

    [Fact]
    public void FollowPlan_IgnoresSnapshotDirectoriesOutsideLiveSet()
    {
        var project = Project with { AuthorizedDirectories = [Project.PrimaryDirectory] };
        Assert.Null(AutomationSnapshotReview.Evaluate(
            Plan(Snapshot(), follow: true), project, Provider, Model));
    }

    [Theory]
    [InlineData("snapshot", AutomationSnapshotReview.MissingSnapshot)]
    [InlineData("mismatch", AutomationSnapshotReview.Mismatch)]
    [InlineData("project", AutomationSnapshotReview.ProjectMissing)]
    [InlineData("provider", AutomationSnapshotReview.ProviderMissing)]
    [InlineData("model", AutomationSnapshotReview.ModelMissing)]
    public void FollowPlan_StillBlocksStructuralProblems(string problem, string expected)
    {
        var schedule = problem switch
        {
            "snapshot" => Plan(null, follow: true),
            "mismatch" => Plan(Snapshot(), follow: true) with { ModelId = Guid.NewGuid() },
            _ => Plan(Snapshot(), follow: true)
        };
        var result = AutomationSnapshotReview.Evaluate(schedule,
            problem == "project" ? null : Project,
            problem == "provider" ? null : Provider,
            problem == "model" ? null : Model);
        Assert.Equal(expected, result);
    }
}
