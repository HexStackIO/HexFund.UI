using Android.App;
using Android.Content;
using Microsoft.Identity.Client;

namespace HexFund.UI.Platforms.Android;

// ── IMPORTANT ────────────────────────────────────────────────────────────────
// The DataScheme below MUST match the format: msal{ClientId}
// where ClientId is the Application (client) ID of your Entra CIAM app registration.
// Update this value whenever the app registration ClientId changes.
// It must also match the android:scheme in AndroidManifest.xml.
// ─────────────────────────────────────────────────────────────────────────────
[Activity(Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryBrowsable, Intent.CategoryDefault },
    DataHost = "auth",
    DataScheme = "msal6c4057fd-e862-48f9-b84d-6f3e2b454c66")]
public class MsalActivity : BrowserTabActivity
{
}
