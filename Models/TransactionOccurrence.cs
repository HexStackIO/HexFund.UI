using System.Text.Json;

namespace HexFund.UI.Models;

public class TransactionOccurrence
{
    public Guid TransactionId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime OccurrenceDate { get; set; }
    public FrequencyType Frequency { get; set; }
    public string? Color { get; set; }
    public string AmountDisplay => Amount.ToString("C");
    public Color AmountColor =>
        Color != null ? Microsoft.Maui.Graphics.Color.FromArgb(Color)
                      : (Amount >= 0 ? Colors.Green : Colors.Red);
    public string CategoryDisplay =>
        string.IsNullOrEmpty(Category) ? "Uncategorized" : Category;
}