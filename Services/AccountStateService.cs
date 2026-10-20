namespace HexFund.UI.Services;

using HexFund.UI.Models;

/// <summary>
/// Holds the currently selected account and signals cross-view-model events. Registered as a
/// singleton so every page sees the same selection.
/// </summary>
public interface IAccountStateService
{
    Account? SelectedAccount { get; set; }

    /// <summary>Raised when the selected account changes.</summary>
    event Action? SelectedAccountChanged;

    /// <summary>
    /// Raised whenever a transaction is created, updated, deleted or amended, so views that show
    /// derived data (such as the calendar) can refresh.
    /// </summary>
    event Action? TransactionsChanged;

    /// <summary>Signals that transaction data has changed for the current account.</summary>
    void NotifyTransactionsChanged();
}

public class AccountStateService : IAccountStateService
{
    private Account? _selectedAccount;

    public Account? SelectedAccount
    {
        get => _selectedAccount;
        set
        {
            if (_selectedAccount == value) return;
            _selectedAccount = value;
            SelectedAccountChanged?.Invoke();
            System.Diagnostics.Debug.WriteLine(
                $"Selected account changed to: {value?.AccountName ?? "null"}");
        }
    }

    public event Action? SelectedAccountChanged;
    public event Action? TransactionsChanged;

    public void NotifyTransactionsChanged()
    {
        System.Diagnostics.Debug.WriteLine("TransactionsChanged raised");
        TransactionsChanged?.Invoke();
    }
}
