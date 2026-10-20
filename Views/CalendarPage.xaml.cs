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

        // The platform handler intercepts swipes at the native list level, so they fire even when
        // the finger starts on a calendar cell, which a MAUI gesture recognizer cannot do.
        CalendarCollectionView.SwipedLeft  += (_, _) => _viewModel.NextMonthCommand.Execute(null);
        CalendarCollectionView.SwipedRight += (_, _) => _viewModel.PreviousMonthCommand.Execute(null);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeAsync();
    }
}
