using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HexFund.UI.Models;

namespace HexFund.UI.ViewModels;

/// <summary>
/// Thin wrapper around a <see cref="Transaction"/> that adds the per-card
/// UI state needed for the amendment history accordion.
///
/// Keeps the CollectionView ItemsSource strongly typed and avoids any
/// code-behind tricks — IsExpanded and Predecessors are fully observable.
/// </summary>
public partial class TransactionCardViewModel : ObservableObject
{
    // ── Wrapped transaction (pass-through properties used directly in XAML) ──
    public Transaction Transaction { get; }

    // Convenience pass-throughs so XAML binds directly without .Transaction.X
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

    // ── Accordion state ───────────────────────────────────────────────────────
    [ObservableProperty] private bool isExpanded;
    [ObservableProperty] private List<TransactionCardViewModel> predecessors = new();

    /// <summary>True when this transaction has at least one predecessor to show.</summary>
    public bool HasHistory => Predecessors.Count > 0;

    // ── Parent VM reference (needed to fire commands) ─────────────────────────
    private readonly TransactionsViewModel _parentVm;

    public TransactionCardViewModel(Transaction transaction,
                                    TransactionsViewModel parentVm,
                                    List<Transaction> predecessorTransactions)
    {
        Transaction = transaction;
        _parentVm   = parentVm;

        // Build predecessor card VMs (no further nesting — predecessors don't expand)
        Predecessors = predecessorTransactions
            .Select(p => new TransactionCardViewModel(p, parentVm, new List<Transaction>()))
            .ToList();

        OnPropertyChanged(nameof(HasHistory));
    }

    // ── Commands ──────────────────────────────────────────────────────────────

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
