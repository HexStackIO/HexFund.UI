namespace HexFund.UI.Controls;

/// <summary>
/// A CollectionView that raises left and right swipe events even though the native scroll
/// container would normally consume those touches. The platform handlers intercept touches on
/// Android and add a swipe gesture recognizer on iOS.
/// </summary>
public class SwipeableCollectionView : CollectionView
{
    /// <summary>Raised when the user swipes left across the collection.</summary>
    public event EventHandler? SwipedLeft;

    /// <summary>Raised when the user swipes right across the collection.</summary>
    public event EventHandler? SwipedRight;

    // Called by the platform handlers.
    internal void RaiseSwipedLeft()  => SwipedLeft?.Invoke(this, EventArgs.Empty);
    internal void RaiseSwipedRight() => SwipedRight?.Invoke(this, EventArgs.Empty);
}
