namespace HexFund.UI.Config;

public class EntraAuthConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string TenantDomain { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();

    public string Authority =>
        $"https://financeplannerapp.ciamlogin.com/{TenantId}";

    public string RedirectUri =>
        $"msal{ClientId}://auth";
}
