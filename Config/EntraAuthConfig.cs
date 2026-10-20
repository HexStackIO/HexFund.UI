namespace HexFund.UI.Config;

/// <summary>
/// Microsoft Entra External ID (CIAM) settings used by MSAL. MauiProgram fills it from
/// AuthSecrets, which is generated at build time and is not committed.
/// </summary>
public class EntraAuthConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string TenantDomain { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();

    /// <summary>The CIAM authority URL for the configured tenant.</summary>
    public string Authority =>
        $"https://financeplannerapp.ciamlogin.com/{TenantId}";

    /// <summary>The custom-scheme redirect URI, which must match the app registration.</summary>
    public string RedirectUri =>
        $"msal{ClientId}://auth";
}
