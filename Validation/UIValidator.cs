using System.Text.RegularExpressions;
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

    // Shared example hint used in all numeric error messages for consistency
    private const string NumericHint = "e.g. 100.00";

    // ── Account ───────────────────────────────────────────────────────────────

    public static string? ValidateAccountName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Account name is required.";

        var trimmed = value.Trim();

        if (trimmed.Length > AccountNameMaxLength)
            return $"Account name cannot exceed {AccountNameMaxLength} characters.";

        if (ContainsUnsafeContent(trimmed))
            return "Account name contains invalid characters.";

        return null;
    }

    /// <summary>
    /// Checks a proposed account name against the existing account list.
    /// Case-insensitive. Pass the current account's ID when editing to allow
    /// keeping the same name unchanged.
    /// </summary>
    public static string? ValidateAccountNameUnique(
        string? value,
        IEnumerable<Models.Account> existingAccounts,
        Guid? currentAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null; // structural check handled elsewhere

        var trimmed = value.Trim();

        var duplicate = existingAccounts.FirstOrDefault(a =>
            string.Equals(a.AccountName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase) &&
            a.AccountId != currentAccountId);

        return duplicate != null
            ? $"An account named \"{duplicate.AccountName}\" already exists."
            : null;
    }

    public static string? ValidateInitialBalance(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Starting balance is required.";

        if (!decimal.TryParse(value.Trim(), out var amount))
            return $"Please enter a valid number ({NumericHint}).";

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

        if (ContainsUnsafeContent(trimmed))
            return "Description contains invalid characters.";

        return null;
    }

    public static string? ValidateAmount(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "Amount is required.";

        if (!decimal.TryParse(value.Trim(), out var amount))
            return $"Please enter a valid number ({NumericHint}).";

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

        if (ContainsUnsafeContent(trimmed))
            return "Category contains invalid characters.";

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

        if (ContainsUnsafeContent(trimmed))
            return "Description contains invalid characters.";

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

        if (ContainsUnsafeContent(trimmed))
            return $"{fieldLabel} contains invalid characters.";

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

        if (ContainsUnsafeContent(trimmed))
            return "Category name contains invalid characters.";

        return null;
    }

    // ── Numeric input sanitization ────────────────────────────────────────────

    /// <summary>
    /// Strips any character that is not a digit, a leading minus sign, or a single
    /// decimal point. Also enforces a maximum of 2 decimal places.
    /// Safe to call on every keystroke from an Entry.TextChanged handler.
    /// </summary>
    public static string SanitizeDecimalInput(string? raw, bool allowNegative = false)
    {
        if (string.IsNullOrEmpty(raw)) return string.Empty;

        var sb = new System.Text.StringBuilder(raw.Length);
        bool hasDot     = false;
        int  decimals   = 0;
        bool leadingPos = true;

        foreach (var ch in raw)
        {
            if (allowNegative && ch == '-' && sb.Length == 0)
            {
                sb.Append(ch);
                leadingPos = false;
                continue;
            }

            if (ch == '.' && !hasDot)
            {
                hasDot    = true;
                leadingPos = false;
                sb.Append(ch);
                continue;
            }

            if (char.IsDigit(ch))
            {
                if (hasDot)
                {
                    if (decimals < 2)
                    {
                        sb.Append(ch);
                        decimals++;
                    }
                    // silently drop extra decimal places
                }
                else
                {
                    sb.Append(ch);
                    leadingPos = false;
                }
            }
            // all other characters (symbols, letters, SQL punctuation) are dropped
        }

        return sb.ToString();
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Detects HTML markup, script injection patterns, and common SQL injection
    /// sequences. This is a defense-in-depth layer — the API remains the
    /// authoritative guard.
    /// </summary>
    private static bool ContainsUnsafeContent(string input)
    {
        // HTML / script tags
        if (input.Contains('<') || input.Contains('>'))
            return true;

        // SQL comment sequences
        if (input.Contains("--") || input.Contains("/*") || input.Contains("*/"))
            return true;

        // SQL statement keywords preceded by a separator (space, semicolon, parenthesis)
        // Pattern: matches "; DROP", "' OR", "1=1", etc.
        if (Regex.IsMatch(input,
            @"[;'""`]\s*(DROP|DELETE|INSERT|UPDATE|SELECT|UNION|EXEC|EXECUTE|ALTER|CREATE|TRUNCATE)\b",
            RegexOptions.IgnoreCase))
            return true;

        // Bare semicolons (no legitimate use in any of our text fields)
        if (input.Contains(';'))
            return true;

        return false;
    }
}
