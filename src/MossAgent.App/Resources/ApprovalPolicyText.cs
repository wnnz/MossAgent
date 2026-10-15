using System.Globalization;
using System.Resources;
using MossAgent.Domain;

namespace MossAgent.App.Resources;

public static class ApprovalPolicyText
{
    private static readonly ResourceManager Labels = new(
        "MossAgent.App.Resources.ApprovalPolicyStrings", typeof(ApprovalPolicyText).Assembly);

    public static string Label(ApprovalPolicy policy) => Get(Enum.IsDefined(policy) ? policy.ToString() : "UnknownPolicy");

    internal static string Get(string key) => Labels.GetString(key, CultureInfo.CurrentUICulture)
        ?? throw new InvalidOperationException($"Missing approval policy resource: {key}");
}
