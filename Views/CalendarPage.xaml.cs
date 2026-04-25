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
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // InitializeAsync now always force-refreshes to catch transaction changes
        _viewModel.InitializeAsync();
    }
}
