using HexFund.UI.ViewModels;

namespace HexFund.UI.Views;

public partial class FeedbackPage : ContentPage
{
    private readonly FeedbackViewModel _viewModel;

    public FeedbackPage(FeedbackViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Initialize();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.FlushDraft();
    }
}
