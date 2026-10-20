using Android.Views;
using AndroidX.RecyclerView.Widget;
using HexFund.UI.Controls;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using AndroidContext = Android.Content.Context;

namespace HexFund.UI.Platforms.Android;

/// <summary>
/// Android handler for <see cref="SwipeableCollectionView"/>. It does not override
/// CreatePlatformView, because that would discard the adapter, layout manager and other setup
/// that the base handler applies. Instead ConnectHandler receives the configured RecyclerView and
/// adds an OnItemTouchListener, the RecyclerView API for intercepting touches before item click
/// handlers run. Horizontal flings raise SwipedLeft and SwipedRight; taps and vertical scrolling
/// pass through.
/// </summary>
public class SwipeableCollectionViewHandler : CollectionViewHandler
{
    private SwipeTouchListener? _touchListener;

    protected override void ConnectHandler(RecyclerView platformView)
    {
        base.ConnectHandler(platformView);

        _touchListener = new SwipeTouchListener(
            platformView.Context!,
            onSwipedLeft:  () => (VirtualView as SwipeableCollectionView)?.RaiseSwipedLeft(),
            onSwipedRight: () => (VirtualView as SwipeableCollectionView)?.RaiseSwipedRight());

        platformView.AddOnItemTouchListener(_touchListener);
    }

    protected override void DisconnectHandler(RecyclerView platformView)
    {
        if (_touchListener != null)
            platformView.RemoveOnItemTouchListener(_touchListener);

        base.DisconnectHandler(platformView);
    }
}

/// <summary>
/// Intercepts horizontal flings. OnInterceptTouchEvent sees every MotionEvent before RecyclerView
/// dispatches it to its children or its own scrolling.
/// </summary>
internal sealed class SwipeTouchListener : Java.Lang.Object, RecyclerView.IOnItemTouchListener
{
    private const float MinDistanceDp  = 40f;
    private const float MaxOffAxisDp   = 60f;
    private const float MinVelocityDp  = 80f;

    private readonly GestureDetector _detector;
    private bool _intercepting;

    public SwipeTouchListener(
        AndroidContext context,
        Action onSwipedLeft,
        Action onSwipedRight)
    {
        var density = context.Resources?.DisplayMetrics?.Density ?? 1f;

        _detector = new GestureDetector(context,
            new FlingListener(
                MinDistanceDp * density,
                MaxOffAxisDp  * density,
                MinVelocityDp * density,
                onSwipedLeft,
                onSwipedRight,
                intercepting => _intercepting = intercepting));
    }

    // Returning true takes the rest of the gesture away from the children.
    public bool OnInterceptTouchEvent(RecyclerView rv, MotionEvent e)
    {
        _detector.OnTouchEvent(e);
        return _intercepting;
    }

    public void OnTouchEvent(RecyclerView rv, MotionEvent e)
    {
        _detector.OnTouchEvent(e);
    }

    public void OnRequestDisallowInterceptTouchEvent(bool disallowIntercept) { }

    private sealed class FlingListener : GestureDetector.SimpleOnGestureListener
    {
        private readonly float _minDist;
        private readonly float _maxOffAxis;
        private readonly float _minVelocity;
        private readonly Action _left;
        private readonly Action _right;
        private readonly Action<bool> _setIntercepting;

        public FlingListener(
            float minDist, float maxOffAxis, float minVelocity,
            Action left, Action right,
            Action<bool> setIntercepting)
        {
            _minDist         = minDist;
            _maxOffAxis      = maxOffAxis;
            _minVelocity     = minVelocity;
            _left            = left;
            _right           = right;
            _setIntercepting = setIntercepting;
        }

        public override bool OnDown(MotionEvent e)
        {
            _setIntercepting(false);
            return false; // Let RecyclerView see ACTION_DOWN.
        }

        public override bool OnFling(
            MotionEvent? e1, MotionEvent e2,
            float velocityX, float velocityY)
        {
            if (e1 == null) return false;

            float dx = e2.RawX - e1.RawX;
            float dy = e2.RawY - e1.RawY;

            if (Math.Abs(dy) > _maxOffAxis)        return false;
            if (Math.Abs(dx) < _minDist)           return false;
            if (Math.Abs(velocityX) < _minVelocity) return false;

            // Takes the remaining events of this gesture so the list does not also scroll.
            _setIntercepting(true);

            if (dx < 0) _left();
            else        _right();

            return true;
        }
    }
}
