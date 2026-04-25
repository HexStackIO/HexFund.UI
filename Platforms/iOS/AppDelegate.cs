using Foundation;
using Microsoft.Maui;
using Microsoft.Maui.Hosting;

namespace HexFund.UI;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
