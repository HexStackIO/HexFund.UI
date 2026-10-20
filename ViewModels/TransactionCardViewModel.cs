using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;

namespace HexFund.UI.ViewModels;

/// <summary>
/// Wraps a <see cref="Transaction"/> with the per-card state for the amendment history
/// accordion, so the list can bind to a strongly typed, observable item.
/// </summary>
public partial class TransactionCardViewModel : ObservableObject
{
    public Transaction Transaction { get; }

    // Pass-throughs so XAML can bind to the properties directly.
    public Guid        TransactionId      => Transaction.TransactionId;
    public string      Description        => Transaction.Description;
    public string      AmountDisplay      => Transaction.AmountDisplay;
    public string      DateRangeDisplay   => Transaction.DateRangeDisplay;
    public string      FrequencyDisplay   => Transaction.FrequencyDisplay;
    public string?     Category           => Transaction.Category;
    public bool        HasCategory        => Transaction.HasCategory;
    public bool        IsActive           => Transaction.IsActive;
    public bool        IsSuperseded       => Transaction.IsSuperseded;
    public Microsoft.Maui.Graphics.Color AmountColor     => Transaction.AmountColor;
    public Microsoft.Maui.Graphics.Color CardBorderColor => Transaction.CardBorderColor;

    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private List<TransactionCardViewModel> predecessors = new();

    /// <summary>True when this transaction has at least one predecessor to show.</summary>
    public bool HasHistory => Predecessors.Count > 0;

    private readonly TransactionsViewModel _parentVm;

    public TransactionCardViewModel(Transaction transaction,
                                    TransactionsViewModel parentVm,
                                    List<Transaction> predecessorTransactions)
    {
        Transaction = transaction;
        _parentVm   = parentVm;

        // Predecessors do not nest further, so they get no history of their own.
        Predecessors = predecessorTransactions
            .Select(p => new TransactionCardViewModel(p, parentVm, new List<Transaction>()))
            .ToList();

        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void ToggleHistory()
    {
        IsExpanded = !IsExpanded;
        OnPropertyChanged(nameof(HasHistory));
    }

    [RelayCommand]
    private void OpenEdit() =>
        _parentVm.ShowEditTransactionCommand.Execute(Transaction);

    [RelayCommand]
    private void OpenAmend() =>
        _parentVm.ShowAmendTransactionCommand.Execute(Transaction);

    [RelayCommand]
    private async Task ToggleActiveAsync() =>
        await _parentVm.ToggleActiveCommand.ExecuteAsync(Transaction);

    [RelayCommand]
    private async Task DeleteAsync() =>
        await _parentVm.DeleteTransactionCommand.ExecuteAsync(Transaction);

    [RelayCommand]
    private void EditPredecessor() =>
        _parentVm.EditPredecessorCommand.Execute(Transaction);

    [RelayCommand]
    private async Task RestoreAsync() =>
        await _parentVm.RestoreTransactionCommand.ExecuteAsync(Transaction);
}
