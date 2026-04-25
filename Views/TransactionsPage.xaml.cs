using HexFund.UI.ViewModels;
using HexFund.UI.Models;

namespace HexFund.UI.Views;

public partial class TransactionsPage : ContentPage
{
    private readonly TransactionsViewModel _viewModel;

    public TransactionsPage(TransactionsViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = _viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.InitializeAsync();
    }

    private async void OnItemLongPress(object? sender, PointerEventArgs e)
    {
        if (sender is Border border && border.BindingContext is Transaction transaction)
        {
            var action = await DisplayActionSheet(
                $"{transaction.Description}",
                "Cancel",
                "Delete",
                "Edit");

            switch (action)
            {
                case "Delete":
                    if (_viewModel.DeleteTransactionCommand.CanExecute(transaction))
                        await _viewModel.DeleteTransactionCommand.ExecuteAsync(transaction);
                    break;
                    
                case "Edit":
                    if (_viewModel.ShowEditTransactionCommand.CanExecute(transaction))
                        _viewModel.ShowEditTransactionCommand.Execute(transaction);
                    break;
            }
        }
    }
}
