namespace HexFund.UI.Models;

/// <summary>
/// Client-side representation of a user-defined transaction category.
/// </summary>
public class UserCategory
{
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
}