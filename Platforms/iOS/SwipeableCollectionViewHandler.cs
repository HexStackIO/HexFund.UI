using HexFund.UI.Controls;
using Microsoft.Maui.Controls.Handlers.Items;
using Microsoft.Maui.Handlers;
using UIKit;

namespace HexFund.UI.Platforms.iOS;

/// <summary>
/// iOS handler for SwipeableCollectionView.
/// Adds UISwipeGestureRecognizers directly to the native UICollectionView.
/// cancelsTouchesInView = false ensures existing tap recognizers still fire.
/// </summary>
public class SwipeableCollectionViewHandler : CollectionViewHandler
{
    private UISwipeGestureRecognizer? _swipeLeft;
    private UISwipeGestureRecognizer? _swipeRight;

    protected override void ConnectHandler(UIView platformView)
    {
        base.ConnectHandler(platformView);

        _swipeLeft = new UISwipeGestureRecognizer(() =>
        {
            if (VirtualView is SwipeableCollectionView sv) sv.RaiseSwipedLeft();
        })
        {
            Direction             = UISwipeGestureRecognizerDirection.Left,
            CancelsTouchesInView  = false,
        };

        _swipeRight = new UISwipeGestureRecognizer(() =>
        {
            if (VirtualView is SwipeableCollectionView sv) sv.RaiseSwipedRight();
        })
        {
            Direction             = UISwipeGestureRecognizerDirection.Right,
            CancelsTouchesInView  = false,
        };

        platformView.AddGestureRecognizer(_swipeLeft);
        platformView.AddGestureRecognizer(_swipeRight);
    }

    protected override void DisconnectHandler(UIView platformView)
    {
        if (_swipeLeft  != null) platformView.RemoveGestureRecognizer(_swipeLeft);
        if (_swipeRight != null) platformView.RemoveGestureRecognizer(_swipeRight);
        base.DisconnectHandler(platformView);
    }
}
