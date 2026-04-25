namespace HexFund.UI.Models;

public class CreateAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public decimal InitialBalance { get; set; }
    public string Currency { get; set; } = "USD";
}

public class UpdateAccountRequest
{
    public string AccountName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class CreateTransactionRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public FrequencyType Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? Color { get; set; }
}

public class UpdateTransactionRequest
{
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Category { get; set; }
    public FrequencyType Frequency { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; }
    public string? Color { get; set; }
}

public class AmendTransactionRequest
{
    public DateTime EffectiveDate { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? Color { get; set; }
}

public class UpdateProfileRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class CreateCategoryRequest
{
    public string Name { get; set; } = string.Empty;
}