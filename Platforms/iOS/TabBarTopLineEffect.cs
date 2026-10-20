using CoreGraphics;
using Microsoft.Maui.LifecycleEvents;
using UIKit;

namespace HexFund.UI.Platforms.iOS;

/// <summary>
/// Draws a 3pt white line at the top of the active tab and nudges the middle tabs apart to leave
/// room for the floating button. <see cref="Reattach"/> must run when the shell swaps the visible
/// tab bar, so both the login and main tab bars are covered.
/// </summary>
public static class TabBarTopLineEffect
{
    private const string IndicatorTag = "FP_TopLine";
    private const float LineHeight = 3f;

    /// <summary>
    /// Call after every CurrentItem change in AppShell, so the effect finds the new
    /// UITabBarController that MAUI creates.
    /// </summary>
    public static void Reattach()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tbc = FindTabBarController(GetRootViewController());
            if (tbc == null) return;
            ApplyTabNudges(tbc.TabBar);
            DrawTopLine(tbc.TabBar);
            tbc.ViewControllerSelected -= OnViewControllerSelected;
            tbc.ViewControllerSelected += OnViewControllerSelected;
        });
    }

    public static void Register(ILifecycleBuilder lifecycle)
    {
        lifecycle.AddiOS(ios =>
        {
            ios.FinishedLaunching((_, _) =>
            {
                MainThread.BeginInvokeOnMainThread(Reattach);
                return true;
            });
        });
    }

    private static void OnViewControllerSelected(object? sender, UITabBarSelectionEventArgs e)
    {
        if (sender is UITabBarController tbc)
            MainThread.BeginInvokeOnMainThread(() => DrawTopLine(tbc.TabBar));
    }

    private static UIViewController? GetRootViewController()
        => UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIWindowScene>()
            .FirstOrDefault()
            ?.Windows
            .FirstOrDefault(w => w.IsKeyWindow)
            ?.RootViewController;

    private static UITabBarController? FindTabBarController(UIViewController? vc)
    {
        if (vc == null) return null;
        if (vc is UITabBarController tbc) return tbc;
        foreach (var child in vc.ChildViewControllers)
        {
            var found = FindTabBarController(child);
            if (found != null) return found;
        }
        return null;
    }

    // Horizontal nudge in points: Calendar moves left and Ledger moves right.
    private const float NudgePt = 10f;

    /// <summary>
    /// Moves Calendar (index 1) left and Ledger (index 2) right so the floating button has room.
    /// </summary>
    private static void ApplyTabNudges(UITabBar tabBar)
    {
        var items = tabBar.Items;
        if (items == null || items.Length < 3) return;

        items[1].ImageInsets = new UIEdgeInsets(0, -NudgePt, 0, NudgePt);
        items[1].TitlePositionAdjustment = new UIOffset(-NudgePt, 0);

        items[2].ImageInsets = new UIEdgeInsets(0, NudgePt, 0, -NudgePt);
        items[2].TitlePositionAdjustment = new UIOffset(NudgePt, 0);
    }

    private static void DrawTopLine(UITabBar tabBar)
    {
        foreach (var sub in tabBar.Subviews)
        {
            if (sub.AccessibilityIdentifier == IndicatorTag)
                sub.RemoveFromSuperview();
        }

        var items = tabBar.Items;
        if (items == null || items.Length == 0) return;

        nfloat tabWidth = tabBar.Frame.Width / items.Length;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] != tabBar.SelectedItem) continue;

            var line = new UIView(new CGRect(
                x: tabWidth * i,
                y: 0,
                width: tabWidth,
                height: LineHeight))
            {
                BackgroundColor = UIColor.White,
                AccessibilityIdentifier = IndicatorTag,
                UserInteractionEnabled = false
            };

            tabBar.AddSubview(line);
            tabBar.BringSubviewToFront(line);
            break;
        }
    }
}