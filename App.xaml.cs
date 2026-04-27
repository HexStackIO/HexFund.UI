using HexFund.UI.Services;

namespace HexFund.UI;

public partial class App : Application
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly IAccountStateService _accountStateService;

    private AppShell? _shell;
    private bool _fabInjected;

    // Native view references for show/hide
#if ANDROID
    private global::Android.Widget.FrameLayout? _androidFabFrame;
#endif
#if IOS
    private UIKit.UIImageView? _iosFabView;
#endif

    public App(
        IAuthService authService,
        ISettingsService settingsService,
        IAccountStateService accountStateService)
    {
        _authService         = authService;
        _settingsService     = settingsService;
        _accountStateService = accountStateService;

        InitializeComponent();
        ThemeService.Apply(_settingsService.Theme);

        // Only show FAB when authenticated — inject on first login, toggle after
        _authService.AuthStateChanged += () =>
            MainThread.BeginInvokeOnMainThread(OnAuthStateChanged);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _shell = new AppShell(_authService, _settingsService, _accountStateService);
        return new Window(_shell);
    }

    // ── Auth state ────────────────────────────────────────────────────────────

    private void OnAuthStateChanged()
    {
        if (_authService.IsAuthenticated)
        {
            if (!_fabInjected)
            {
                _fabInjected = true;
                // Short delay so the authenticated Shell page is fully rendered
                // before we add the overlay — avoids injecting over the login UI
                MainThread.BeginInvokeOnMainThread(async () =>
                {
                    await Task.Delay(300);
                    InjectHexFab();
                });
            }
            else
            {
                SetFabVisible(true);
            }
        }
        else
        {
            SetFabVisible(false);
        }
    }

    private void SetFabVisible(bool visible)
    {
#if ANDROID
        if (_androidFabFrame != null)
            _androidFabFrame.Visibility = visible
                ? global::Android.Views.ViewStates.Visible
                : global::Android.Views.ViewStates.Gone;
#endif
#if IOS
        if (_iosFabView != null)
            _iosFabView.Hidden = !visible;
#endif
    }

    // ── Hex FAB injection ─────────────────────────────────────────────────────

    private const double FabSize   = 62.0;
    private const double FabBottom = 48.0;

    private void InjectHexFab()
    {
#if ANDROID
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity?.Window?.DecorView is not global::Android.Views.ViewGroup decorView)
            return;

        float density = activity.Resources?.DisplayMetrics?.Density ?? 1f;
        int sizePx   = (int)(FabSize * density);
        int bottomPx = (int)(FabBottom * density);

        var frame = new global::Android.Widget.FrameLayout(activity)
        {
            Clickable = false, // frame itself doesn't consume — only the image does
        };

        var hexImg = new global::Android.Widget.ImageView(activity);
        var resId  = activity.Resources?.GetIdentifier(
                         "hex_fab", "mipmap", activity.PackageName) ?? 0;
        if (resId == 0)
            resId = activity.Resources?.GetIdentifier(
                        "hex_fab", "drawable", activity.PackageName) ?? 0;
        if (resId != 0)
            hexImg.SetImageResource(resId);

        hexImg.Clickable = true;
        hexImg.Click += async (_, _) =>
        {
            // Disable immediately and synchronously before any await so rapid
            // taps cannot queue up multiple navigation calls
            hexImg.Clickable = false;
            try
            {
                if (_shell != null)
                    await _shell.ExecuteHexFabAsync();
            }
            finally
            {
                // Re-enable after navigation completes
                hexImg.Clickable = true;
            }
        };

        var imgLp = new global::Android.Widget.FrameLayout.LayoutParams(sizePx, sizePx)
        {
            Gravity      = global::Android.Views.GravityFlags.Bottom |
                           global::Android.Views.GravityFlags.CenterHorizontal,
            BottomMargin = bottomPx,
        };
        frame.AddView(hexImg, imgLp);

        // The FrameLayout covers the full screen but only the ImageView is
        // Clickable=true. The frame has Clickable=false so all touches that
        // don't land on the ImageView fall straight through to views below.
        var rootLp = new global::Android.Views.ViewGroup.LayoutParams(
            global::Android.Views.ViewGroup.LayoutParams.MatchParent,
            global::Android.Views.ViewGroup.LayoutParams.MatchParent);

        decorView.AddView(frame, rootLp);
        _androidFabFrame = frame;

        StartAndroidPulse(hexImg);

#elif IOS
        var scene = UIKit.UIApplication.SharedApplication
            .ConnectedScenes
            .OfType<UIKit.UIWindowScene>()
            .FirstOrDefault();
        var uiWindow = scene?.Windows.FirstOrDefault(w => w.IsKeyWindow)
                       ?? UIKit.UIApplication.SharedApplication.KeyWindow;
        if (uiWindow == null) return;

        nfloat sizePt   = (nfloat)FabSize;
        nfloat bottomPt = (nfloat)FabBottom;
        nfloat screenW  = uiWindow.Bounds.Width;
        nfloat screenH  = uiWindow.Bounds.Height;

        var hexImg = new UIKit.UIImageView
        {
            Image = UIKit.UIImage.FromBundle("hex_fab")
                    ?? UIKit.UIImage.FromBundle("hex_fab.png"),
            ContentMode            = UIKit.UIViewContentMode.ScaleAspectFit,
            Frame                  = new CoreGraphics.CGRect(
                (screenW - sizePt) / 2,
                screenH - bottomPt - sizePt,
                sizePt, sizePt),
            UserInteractionEnabled = true,
        };

        var tap = new UIKit.UITapGestureRecognizer();
        tap.AddTarget(async () =>
        {
            hexImg.UserInteractionEnabled = false;
            try
            {
                if (_shell != null)
                    await _shell.ExecuteHexFabAsync();
            }
            finally
            {
                hexImg.UserInteractionEnabled = true;
            }
        });
        hexImg.AddGestureRecognizer(tap);

        uiWindow.AddSubview(hexImg);
        uiWindow.BringSubviewToFront(hexImg);
        _iosFabView = hexImg;

        StartiOSPulse(hexImg);
#endif
    }

    // ── Animations ────────────────────────────────────────────────────────────

#if ANDROID
    private static void StartAndroidPulse(global::Android.Widget.ImageView view)
    {
        var animator = Android.Animation.ValueAnimator.OfFloat(0.55f, 1.0f)!;
        animator.SetDuration(950);
        animator.RepeatCount = Android.Animation.ValueAnimator.Infinite;
        animator.RepeatMode  = Android.Animation.ValueAnimatorRepeatMode.Reverse;
        animator.SetInterpolator(
            new Android.Views.Animations.AccelerateDecelerateInterpolator());
        animator.Update += (_, e) =>
        {
            if (view.Handle != IntPtr.Zero)
                view.Alpha = (float)(e.Animation?.AnimatedValue ?? 1f);
        };
        animator.Start();
    }
#endif

#if IOS
    private static void StartiOSPulse(UIKit.UIImageView view)
    {
        void Pulse()
        {
            UIKit.UIView.Animate(0.95, () => view.Alpha = 0.55f,
                () => UIKit.UIView.Animate(0.95, () => view.Alpha = 1.0f, Pulse));
        }
        Pulse();
    }
#endif
}
