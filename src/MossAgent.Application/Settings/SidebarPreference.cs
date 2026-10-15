namespace MossAgent.Application.Settings;

public sealed record SidebarPreference(
    SidebarProjectSortOrder ProjectSortOrder,
    IReadOnlyList<Guid> ManualProjectOrder);
