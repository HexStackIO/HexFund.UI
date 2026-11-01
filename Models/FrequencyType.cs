namespace HexFund.UI.Models;

/// <summary>How often a transaction repeats. Values match the API.</summary>
public enum FrequencyType
{
    Once             = 0,
    Daily            = 1,
    Weekly           = 2,
    BiWeekly         = 3,
    FirstThirdFriday = 4,
    Monthly          = 5,
    BiMonthly        = 6
}
