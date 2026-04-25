namespace HexFund.UI.Models;

public enum TransactionSortField
{
    StartDate   = 0,  // default — matches existing behaviour
    Name        = 1,
    Amount      = 2,
    Recurrence  = 3,
}

public enum SortDirection
{
    Descending = 0,  // default for dates/amounts — most recent / largest first
    Ascending  = 1,
}
