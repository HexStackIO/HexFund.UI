using CoreGraphics;
using Microsoft.Maui.LifecycleEvents;
using UIKit;

namespace HexFund.UI.Platforms.iOS;

/// <summary>
/// Draws a 3pt white line at the top of the active tab.
/// Re-attaches every time AppShell swaps the visible TabBar so both
/// the pre-login (AuthTabBar) and post-login (MainTabBar) are covered.
/// </summary>
public static class TabBarTopLineEffect
{
    private const string IndicatorTag = "FP_TopLine";
    private const float LineHeight = 3f;

    // ── Public API called from AppShell ───────────────────────────────────────

    /// <summary>
    /// Call this after every CurrentItem change in AppShell so the effect
    /// re-discovers the new UITabBarController that MAUI creates.
    /// </summary>
    public static void Reattach()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var tbc = FindTabBarController(GetRootViewController());
            if (tbc == null) return;
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

    // ── UIViewController traversal ────────────────────────────────────────────

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

    // ── Drawing logic ─────────────────────────────────────────────────────────

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