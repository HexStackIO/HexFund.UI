namespace HexFund.UI.Controls;

/// <summary>
/// A CollectionView subclass that exposes left/right swipe events that work
/// even when the native RecyclerView/UICollectionView would normally consume
/// the touch. Platform-specific handlers override OnInterceptTouchEvent (Android)
/// or add a UISwipeGestureRecognizer with requiresExclusiveTouchType=false (iOS)
/// so horizontal flicks are never swallowed by the scroll container.
/// </summary>
public class SwipeableCollectionView : CollectionView
{
    /// <summary>Raised when the user swipes left across the collection.</summary>
    public event EventHandler? SwipedLeft;

    /// <summary>Raised when the user swipes right across the collection.</summary>
    public event EventHandler? SwipedRight;

    // Called by platform handlers — keeps handler code clean.
    internal void RaiseSwipedLeft()  => SwipedLeft?.Invoke(this, EventArgs.Empty);
    internal void RaiseSwipedRight() => SwipedRight?.Invoke(this, EventArgs.Empty);
}
