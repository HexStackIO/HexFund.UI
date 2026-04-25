using HexFund.UI.Models;

namespace HexFund.UI.Validation;

/// <summary>
/// Client-side validation helpers shared across all ViewModels.
///
/// These rules mirror the API-side constraints so users get instant,
/// friendly feedback before a request is ever sent. The API enforces
/// the same rules independently — this layer is UX, not security.
///
/// All methods return a human-readable error string, or null if valid.
/// </summary>
public static class UIValidator
{
    // ── Field limits (must match API DTOs) ────────────────────────────────────
    public const int AccountNameMaxLength      = 25;
    public const int DescriptionMaxLength      = 40;
    public const int CategoryMaxLength         = 25;
    public const int AmendDescriptionMaxLength = 40;

    // ── Account ───────────────────────────────────────────────────────────────

    public static string? ValidateAccountName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Account name is required.";

        var trimmed = value.Trim();

        if (trimmed.Length > AccountNameMaxLength)
            return $"Account name cannot exceed {AccountNameMaxLength} characters.";

        if (ContainsHtml(trimmed))
            return "Account name cannot contain HTML or special markup.";

        return null;
    }

    public static string? ValidateInitialBalance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Starting balance is required.";

        if (!decimal.TryParse(value.Trim(), out var amount))
            return "Please enter a valid number (e.g. 1000.00).";

        if (amount < -1_000_000_000 || amount > 1_000_000_000)
            return "Balance must be between -1,000,000,000 and 1,000,000,000.";

        return null;
    }

    // ── Transaction ───────────────────────────────────────────────────────────

    public static string? ValidateDescription(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Description is required.";

        var trimmed = value.Trim();

        if (trimmed.Length > DescriptionMaxLength)
            return $"Description cannot exceed {DescriptionMaxLength} characters.";

        if (ContainsHtml(trimmed))
            return "Description cannot contain HTML or special markup.";

        return null;
    }

    public static string? ValidateAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Amount is required.";

        if (!decimal.TryParse(value.Trim(), out var amount))
            return "Please enter a valid number (e.g. 100.00).";

        if (amount <= 0)
            return "Amount must be greater than zero.";

        if (amount > 1_000_000_000)
            return "Amount cannot exceed 1,000,000,000.";

        return null;
    }

    public static string? ValidateCategory(string? value)
    {
        // Category is optional — only validate if provided
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        if (trimmed.Length > CategoryMaxLength)
            return $"Category cannot exceed {CategoryMaxLength} characters.";

        if (ContainsHtml(trimmed))
            return "Category cannot contain HTML or special markup.";

        return null;
    }

    public static string? ValidateDateRange(DateTime startDate, DateTime? endDate,
                                             FrequencyType? frequency = null)
    {
        if (endDate.HasValue)
        {
            // For Once transactions end date equal to start date is correct and expected
            bool allowEqual = frequency == FrequencyType.Once;
            if (allowEqual ? endDate.Value < startDate : endDate.Value <= startDate)
                return allowEqual
                    ? "End date cannot be before the start date."
                    : "End date must be after the start date.";

            if (endDate.Value > startDate.AddYears(100))
                return "End date cannot be more than 100 years after the start date.";
        }

        if (startDate < DateTime.Today.AddYears(-50))
            return "Start date cannot be more than 50 years in the past.";

        if (startDate > DateTime.Today.AddYears(50))
            return "Start date cannot be more than 50 years in the future.";

        return null;
    }

    // ── Amend ─────────────────────────────────────────────────────────────────

    public static string? ValidateAmendEffectiveDate(DateTime effectiveDate, DateTime transactionStartDate)
    {
        if (effectiveDate <= transactionStartDate)
            return "Effective date must be after the transaction's start date.";

        if (effectiveDate > DateTime.Today.AddYears(50))
            return "Effective date cannot be more than 50 years in the future.";

        return null;
    }

    public static string? ValidateAmendDescription(string? value)
    {
        // Optional field — only validate if provided
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        if (trimmed.Length > AmendDescriptionMaxLength)
            return $"Description cannot exceed {AmendDescriptionMaxLength} characters.";

        if (ContainsHtml(trimmed))
            return "Description cannot contain HTML or special markup.";

        return null;
    }


    // ── Profile ───────────────────────────────────────────────────────────────

    public static string? ValidateProfileName(string? value, string fieldLabel = "Name")
    {
        if (string.IsNullOrWhiteSpace(value))
            return $"{fieldLabel} is required.";

        var trimmed = value.Trim();

        if (trimmed.Length > 100)
            return $"{fieldLabel} cannot exceed 100 characters.";

        if (ContainsHtml(trimmed))
            return $"{fieldLabel} cannot contain HTML or special markup.";

        return null;
    }

    // ── Category ──────────────────────────────────────────────────────────────

    public static string? ValidateCategoryName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Category name is required.";

        var trimmed = value.Trim();

        if (trimmed.Length > CategoryMaxLength)
            return $"Category name cannot exceed {CategoryMaxLength} characters.";

        if (ContainsHtml(trimmed))
            return "Category name cannot contain HTML or special markup.";

        return null;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Quick check for HTML tag patterns. Not a full sanitizer — the API handles
    /// thorough sanitization. This is here to give instant feedback on obvious mistakes.
    /// </summary>
    private static bool ContainsHtml(string input) =>
        input.Contains('<') || input.Contains('>');
}
