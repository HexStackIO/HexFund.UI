using HexFund.UI.Services;
using HexFund.UI.ViewModels;

namespace HexFund.UI;

/// <summary>
/// Application root. Applies the saved theme, creates the shell window, and owns the floating
/// hexagon action button that is added to the native window while the user is signed in.
/// </summary>
public partial class App : Application
{
    private readonly IAuthService _authService;
    private readonly ISettingsService _settingsService;
    private readonly IAccountStateService _accountStateService;

    private AppShell? _shell;
    private bool _fabInjected;

    // Native view references, kept so the button can be hidden and re-themed.
#if ANDROID
    private global::Android.Widget.FrameLayout? _androidFabFrame;
    private global::Android.Widget.ImageView?   _androidFabImg;
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

        // The button is added on the first sign-in and only shown and hidden after that.
        _authService.AuthStateChanged += () =>
            MainThread.BeginInvokeOnMainThread(OnAuthStateChanged);

        // Re-skins the button when the theme changes.
        _settingsService.SettingsChanged += () =>
            MainThread.BeginInvokeOnMainThread(UpdateFabImage);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        _shell = new AppShell(_authService, _settingsService, _accountStateService);
        return new Window(_shell);
    }

    private void OnAuthStateChanged()
    {
        if (_authService.IsAuthenticated)
        {
            if (!_fabInjected)
            {
                _fabInjected = true;
                // The delay lets the signed-in shell finish rendering, so the button is not added
                // over the login page.
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
            // Preferences are not encrypted, so an unsent feedback draft must not outlive the session.
            FeedbackDraft.Clear();
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

    // Size and distance from the bottom edge, in dp on Android and points on iOS.
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
            Clickable = false, // Only the image takes touches.
        };

        var hexImg = new global::Android.Widget.ImageView(activity);
        LoadAndroidFabImage(hexImg, activity);

        hexImg.Clickable = true;
        hexImg.Click += async (_, _) =>
        {
            // Disabled before the first await so rapid taps cannot queue several navigations.
            hexImg.Clickable = false;
            try
            {
                if (_shell != null)
                    await _shell.ExecuteHexFabAsync();
            }
            finally
            {
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

        // The frame covers the whole window but is not clickable, so touches outside the image
        // pass through to the views below.
        var rootLp = new global::Android.Views.ViewGroup.LayoutParams(
            global::Android.Views.ViewGroup.LayoutParams.MatchParent,
            global::Android.Views.ViewGroup.LayoutParams.MatchParent);

        decorView.AddView(frame, rootLp);
        _androidFabFrame = frame;
        _androidFabImg   = hexImg;

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

        var fabName = ThemeService.GetThemeFab(_settingsService.Theme)
                          .Replace(".png", "");   // FromBundle takes the name without extension.

        var hexImg = new UIKit.UIImageView
        {
            Image = UIKit.UIImage.FromBundle(fabName)
                    ?? UIKit.UIImage.FromBundle("hex_fab"),
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

    // Called when the button is added and whenever settings change.
    private void UpdateFabImage()
    {
#if ANDROID
        var activity = global::Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (_androidFabImg != null && activity != null)
            LoadAndroidFabImage(_androidFabImg, activity);
#endif
#if IOS
        if (_iosFabView != null)
            LoadiOSFabImage(_iosFabView);
#endif
    }

#if ANDROID
    private void LoadAndroidFabImage(
        global::Android.Widget.ImageView view,
        global::Android.App.Activity activity)
    {
        var imageName = ThemeService.GetThemeFab(_settingsService.Theme)
                            .Replace(".png", "");   // GetIdentifier takes the name without extension.

        var resId = activity.Resources?.GetIdentifier(
                        imageName, "mipmap", activity.PackageName) ?? 0;
        if (resId == 0)
            resId = activity.Resources?.GetIdentifier(
                        imageName, "drawable", activity.PackageName) ?? 0;

        // Falls back to the unthemed image when the themed asset is missing.
        if (resId == 0)
        {
            resId = activity.Resources?.GetIdentifier(
                        "hex_fab", "mipmap", activity.PackageName) ?? 0;
            if (resId == 0)
                resId = activity.Resources?.GetIdentifier(
                            "hex_fab", "drawable", activity.PackageName) ?? 0;
        }

        if (resId != 0)
            view.SetImageResource(resId);
    }
#endif

#if IOS
    private void LoadiOSFabImage(UIKit.UIImageView view)
    {
        var imageName = ThemeService.GetThemeFab(_settingsService.Theme)
                            .Replace(".png", "");

        view.Image = UIKit.UIImage.FromBundle(imageName)
                     ?? UIKit.UIImage.FromBundle("hex_fab");
    }
#endif

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
