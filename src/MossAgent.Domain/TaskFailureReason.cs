namespace MossAgent.Domain;

public enum TaskFailureReason
{
    Unknown = 0,
    StreamLineLimit = 1,
    StreamEventLimit = 2,
    InvalidStreamEncoding = 3,
    ProviderHttp = 4,
    InvalidProviderResponse = 5,
    TurnLimit = 6,
    ProviderConnection = 7,
    ProviderTimeout = 8,
    ProviderAuthentication = 9,
    ProviderRateLimit = 10,
    ProviderBlocked = 11,
    ProviderOutputLimit = 12,
    UnsupportedReasoningEffort = 13,
    ProxyAuthentication = 14,
    AttachmentHistory = 15,
    ModelContextCapacity = 16,
    WorkspaceAuthorizationChanged = 17,
    UnsupportedImageInput = 18,
    InvalidModelParameters = 19
}
