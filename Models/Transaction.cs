namespace HexFund.UI.Models;

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

public class Transaction
{
    public Guid TransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public FrequencyType Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
    public Guid? PredecessorTransactionId { get; set; }
    public string FrequencyDisplay => Frequency switch
    {
        FrequencyType.Once             => "Once",
        FrequencyType.Daily            => "Daily",
        FrequencyType.Weekly           => "Weekly",
        FrequencyType.BiWeekly         => "Bi-Weekly",
        FrequencyType.FirstThirdFriday => "1st & 3rd Friday",
        FrequencyType.Monthly          => "Monthly",
        FrequencyType.BiMonthly        => "Bi-Monthly",
        _                              => "Unknown"
    };
    public Color AmountColor =>
        Color != null ? Microsoft.Maui.Graphics.Color.FromArgb(Color)
                      : (Amount >= 0 ? Colors.Green : Colors.Red);
    public Color CardBorderColor =>
        Color != null ? Microsoft.Maui.Graphics.Color.FromArgb(Color)
                      : (Amount >= 0 ? Colors.Green : Colors.Red);

    public string AmountDisplay    => $"{(Amount >= 0 ? "+" : "")}{Amount:C}";
    public bool   HasCategory      => !string.IsNullOrEmpty(Category);
    public string StartDateDisplay => StartDate.ToString("MMM dd, yyyy");
    public string EndDateDisplay   => EndDate.HasValue ? EndDate.Value.ToString("MMM dd, yyyy") : "Ongoing";
    public string DateRangeDisplay =>
        Frequency == FrequencyType.Once
            ? StartDate.ToString("MMM dd, yyyy")           // single date — no range needed
            : EndDate.HasValue
                ? $"{StartDate:MMM dd, yyyy} → {EndDate.Value:MMM dd, yyyy}"
                : $"{StartDate:MMM dd, yyyy} → Ongoing";
    public bool IsSuperseded =>
        PredecessorTransactionId != null &&
        EndDate.HasValue &&
        EndDate.Value < DateTime.Today;
}