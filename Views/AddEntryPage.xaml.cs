using HexFund.UI.ViewModels;

namespace HexFund.UI.Views;

public partial class AddEntryPage : ContentPage
{
    private readonly AddEntryViewModel _viewModel;

    public AddEntryPage(AddEntryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
    }
}
