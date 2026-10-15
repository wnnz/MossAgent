using MossAgent.Domain;

namespace MossAgent.App.ViewModels;

public sealed record ProxySelection(Guid? Id, string Name)
{
    internal static IEnumerable<ProxySelection> FromProfiles(
        IReadOnlyList<ProxyProfile> profiles, string directLabel)
    {
        yield return new ProxySelection(null, directLabel);
        for (var index = 0; index < profiles.Count; index++)
        {
            var profile = profiles[index];
            if (profile.IsEnabled) yield return new ProxySelection(profile.Id, profile.Name);
        }
    }
}

