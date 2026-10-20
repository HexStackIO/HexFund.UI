using Android.App;
using Android.Content;
using Microsoft.Identity.Client;

namespace HexFund.UI.Platforms.Android;

// Receives the redirect that ends the interactive Entra sign-in. The DataScheme must be "msal"
// followed by the client ID of the app registration, and must match EntraAuthConfig.RedirectUri.
// Update it when the client ID changes. The scheme is declared only here, because repeating it in
// AndroidManifest.xml would register it twice and make Android show an app chooser on redirect.
[Activity(Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataHost = "auth",
    DataScheme = "msal6c4057fd-e862-48f9-b84d-6f3e2b454c66")]
public class MsalActivity : BrowserTabActivity
{
}
