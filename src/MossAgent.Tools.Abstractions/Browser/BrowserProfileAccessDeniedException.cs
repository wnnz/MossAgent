namespace MossAgent.Tools.Abstractions.Browser;

public sealed class BrowserProfileAccessDeniedException(string message) : Exception(message)
{
    public const string DeniedErrorCode = "browser_profile_denied";

    public string ErrorCode => DeniedErrorCode;
}
