namespace CodingAgent.Api.Contracts.Settings;

public record SettingDto(string Key, string Value);
public record UpdateSettingRequest(string Key, string Value);
