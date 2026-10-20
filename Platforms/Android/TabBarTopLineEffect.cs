using Android.Views;
using Android.Graphics.Drawables;
using Google.Android.Material.BottomNavigation;
using Microsoft.Maui.LifecycleEvents;

namespace HexFund.UI.Platforms.Android;

/// <summary>
/// Draws a 3dp white line at the top of the active bottom navigation tab and nudges the middle
/// tabs apart to leave room for the floating button. <see cref="Reattach"/> must run when the
/// shell swaps the visible tab bar, so both the login and main tab bars are covered.
/// </summary>
public static class TabBarTopLineEffect
{
    // The listener must be removed before a new one is attached after an auth state change.
    // Otherwise the old listener fires against a disposed BottomNavigationView.
    private static RedrawListener? _activeRedrawListener;

    public static void Reattach()
    {
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity == null) return;

        // Removed before the layout pass that would fire it against the disposed view.
        if (_activeRedrawListener != null)
        {
            activity.Window?.DecorView.ViewTreeObserver?
                .RemoveOnGlobalLayoutListener(_activeRedrawListener);
            _activeRedrawListener = null;
        }

        activity.Window?.DecorView.ViewTreeObserver?
            .AddOnGlobalLayoutListener(new LayoutListener(activity));
    }

    public static void Register(ILifecycleBuilder lifecycle)
    {
        lifecycle.AddAndroid(android =>
        {
            android.OnPostCreate((activity, _) =>
            {
                activity.Window?.DecorView.ViewTreeObserver?
                    .AddOnGlobalLayoutListener(new LayoutListener(activity));
            });

            android.OnResume(activity =>
            {
                // Also dropped on resume, for example when returning from the sign-in browser.
                if (_activeRedrawListener != null)
                {
                    activity.Window?.DecorView.ViewTreeObserver?
                        .RemoveOnGlobalLayoutListener(_activeRedrawListener);
                    _activeRedrawListener = null;
                }
                activity.Window?.DecorView.ViewTreeObserver?
                    .AddOnGlobalLayoutListener(new LayoutListener(activity));
            });
        });
    }

    private sealed class LayoutListener : Java.Lang.Object,
        ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly global::Android.App.Activity _activity;

        public LayoutListener(global::Android.App.Activity activity)
            => _activity = activity;

        public void OnGlobalLayout()
        {
            _activity.Window?.DecorView.ViewTreeObserver?
                .RemoveOnGlobalLayoutListener(this);

            var bottomNav = FindBottomNav(_activity.Window?.DecorView);
            if (bottomNav == null) return;

            ApplyTopLines(bottomNav);

            // Stored so Reattach can remove it before the view is disposed.
            var listener = new RedrawListener(_activity, bottomNav);
            _activeRedrawListener = listener;
            _activity.Window?.DecorView.ViewTreeObserver?
                .AddOnGlobalLayoutListener(listener);
        }

        private static BottomNavigationView? FindBottomNav(global::Android.Views.View? root)
        {
            if (root is BottomNavigationView bnv) return bnv;
            if (root is not ViewGroup vg) return null;
            for (int i = 0; i < vg.ChildCount; i++)
            {
                var found = FindBottomNav(vg.GetChildAt(i));
                if (found != null) return found;
            }
            return null;
        }
    }

    private sealed class RedrawListener : Java.Lang.Object,
        ViewTreeObserver.IOnGlobalLayoutListener
    {
        private readonly global::Android.App.Activity _activity;
        private readonly BottomNavigationView _bottomNav;

        public RedrawListener(global::Android.App.Activity activity, BottomNavigationView bottomNav)
        {
            _activity = activity;
            _bottomNav = bottomNav;
        }

        public void OnGlobalLayout()
        {
            // The view is gone, so stop listening.
            if (_bottomNav.Handle == IntPtr.Zero || !_bottomNav.IsAttachedToWindow)
            {
                _activity.Window?.DecorView.ViewTreeObserver?
                    .RemoveOnGlobalLayoutListener(this);
                if (_activeRedrawListener == this)
                    _activeRedrawListener = null;
                return;
            }

            ApplyTopLines(_bottomNav);
        }
    }

    private static void ApplyTopLines(BottomNavigationView bottomNav)
    {
        var menu = bottomNav.Menu;
        var menuView = FindMenuViewGroup(bottomNav);
        if (menuView == null) return;

        float density = bottomNav.Context?.Resources?.DisplayMetrics?.Density ?? 1f;
        int lineHeightPx = (int)(3 * density);

        // Calendar (index 1) moves left and Ledger (index 2) moves right so the floating button
        // does not overlap either label.
        int nudgePx = (int)(10 * density);

        for (int i = 0; i < menuView.ChildCount; i++)
        {
            var itemView = menuView.GetChildAt(i);
            if (itemView == null) continue;

            bool isSelected = i < menu.Size() && (menu.GetItem(i)?.IsChecked ?? false);

            if (isSelected)
            {
                var line = new GradientDrawable();
                line.SetColor(global::Android.Graphics.Color.White);
                line.SetSize(itemView.Width > 0 ? itemView.Width : 1, lineHeightPx);

                var layer = new LayerDrawable(new Drawable[]
                {
                    new ColorDrawable(global::Android.Graphics.Color.Transparent),
                    line
                });
                layer.SetLayerGravity(1, GravityFlags.Top | GravityFlags.FillHorizontal);
                layer.SetLayerInsetBottom(1,
                    itemView.Height > lineHeightPx ? itemView.Height - lineHeightPx : 0);

                itemView.Background = layer;
            }
            else
            {
                itemView.Background =
                    new ColorDrawable(global::Android.Graphics.Color.Transparent);
            }

            itemView.SetPadding(
                i == 1 ? 0 : (i == 2 ? nudgePx : itemView.PaddingLeft),
                itemView.PaddingTop,
                i == 1 ? nudgePx : (i == 2 ? 0 : itemView.PaddingRight),
                itemView.PaddingBottom);
        }
    }

    private static ViewGroup? FindMenuViewGroup(BottomNavigationView bottomNav)
    {
        for (int i = 0; i < bottomNav.ChildCount; i++)
            if (bottomNav.GetChildAt(i) is ViewGroup vg) return vg;
        return null;
    }
}