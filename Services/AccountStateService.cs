namespace HexFund.UI.Services;

using HexFund.UI.Models;

/// <summary>
/// Singleton service that maintains shared state about the currently
/// selected account and signals cross-ViewModel events.
///
/// Changes from original:
///   - Added TransactionsChanged event (replaces AppState.CalendarNeedsRefresh
///     static flag). TransactionsViewModel raises it after any mutation;
///     CalendarViewModel subscribes to force a refresh on next appearance.
/// </summary>
public interface IAccountStateService
{
    Account? SelectedAccount { get; set; }

    /// <summary>Raised when the selected account changes.</summary>
    event Action? SelectedAccountChanged;

    /// <summary>
    /// Raised by TransactionsViewModel whenever a transaction is created,
    /// updated, deleted, or amended. CalendarViewModel subscribes so it can
    /// force a data refresh without a shared static flag.
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
