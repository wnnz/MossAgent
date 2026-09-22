namespace CodingAgent.Api.Contracts.Skills;

public record SkillDto(int Id, string Name, string Description, string Instructions, bool Enabled);
public record CreateSkillRequest(string Name, string Description, string Instructions, bool Enabled);
public record UpdateSkillRequest(string Name, string Description, string Instructions, bool Enabled);
