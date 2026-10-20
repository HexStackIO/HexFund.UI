namespace HexFund.UI.Models;

/// <summary>
/// A user-defined transaction category.
/// </summary>
public class UserCategory
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}