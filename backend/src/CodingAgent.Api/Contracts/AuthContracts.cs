namespace CodingAgent.Api.Contracts.Auth;

public record AuthStatusResponse(bool NeedsSetup, bool Authenticated);
public record SetupRequest(string Password);
public record LoginRequest(string Password, bool RememberMe);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
