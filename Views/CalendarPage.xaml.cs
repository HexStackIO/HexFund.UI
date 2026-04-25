using HexFund.UI.ViewModels;

namespace HexFund.UI.Views;

public partial class CalendarPage : ContentPage
{
    private readonly CalendarViewModel _viewModel;

    public CalendarPage(CalendarViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;

        // Wire the swipe events raised by the platform-specific handler.
        // The handler intercepts at the native RecyclerView/UICollectionView
        // level so these fire even when the user's finger is directly over a
        // calendar cell — something no MAUI gesture recognizer can achieve.
        CalendarCollectionView.SwipedLeft  += (_, _) => _viewModel.NextMonthCommand.Execute(null);
        CalendarCollectionView.SwipedRight += (_, _) => _viewModel.PreviousMonthCommand.Execute(null);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeAsync();
    }
}
