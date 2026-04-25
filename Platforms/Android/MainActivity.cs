using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Identity.Client;

namespace HexFund.UI;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTask,
    ConfigurationChanges =
        ConfigChanges.ScreenSize |
        ConfigChanges.Orientation |
        ConfigChanges.UiMode |
        ConfigChanges.ScreenLayout |
        ConfigChanges.SmallestScreenSize |
        ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        AuthenticationContinuationHelper.SetAuthenticationContinuationEventArgs(
            requestCode, resultCode, data);
    }

    // Back-press interception is handled at the Shell level (AppShell.xaml.cs)
    // to show a themed in-app exit confirmation overlay instead of a native dialog.
}
