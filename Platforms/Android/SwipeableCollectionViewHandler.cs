using Android.Views;
using AndroidX.RecyclerView.Widget;
using HexFund.UI.Controls;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using AndroidContext = Android.Content.Context;

namespace HexFund.UI.Platforms.Android;

/// <summary>
/// Android handler for SwipeableCollectionView.
///
/// We never override CreatePlatformView — that would discard the adapter,
/// LayoutManager, and all other configuration the base handler applies.
/// Instead ConnectHandler receives the fully-configured RecyclerView and
/// attaches a GestureDetector via an OnItemTouchListener, which is the
/// official RecyclerView API for intercepting touches before item click
/// handlers run. Horizontal flings fire SwipedLeft / SwipedRight; everything
/// else (taps, vertical scroll) is passed through untouched.
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
/// RecyclerView.IOnItemTouchListener that intercepts horizontal flings.
/// OnInterceptTouchEvent is called for every MotionEvent before RecyclerView
/// dispatches it to children or its own scroll logic.
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

    // Called before RecyclerView processes the event.
    // Return true to steal the remainder of the gesture from children.
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

    // ── Fling listener ───────────────────────────────────────────────────────

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
            // Reset intercept flag on every new touch sequence.
            _setIntercepting(false);
            return false; // don't consume — let RecyclerView see ACTION_DOWN
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

            // Tell the listener to intercept the remaining events in this gesture
            _setIntercepting(true);

            if (dx < 0) _left();
            else        _right();

            return true;
        }
    }
}
