using Android.App;
using Microsoft.Maui.ApplicationModel;

namespace HexFund.UI.Platforms.Android;

public class AndroidActivityService
{
    public Activity? GetCurrentActivity() =>
        Platform.CurrentActivity;
}