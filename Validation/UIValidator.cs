using System.Text.RegularExpressions;
using HexFund.UI.Models;

namespace HexFund.UI.Validation;

/// <summary>
/// Client-side validation shared by the view models. The rules mirror the API constraints so users
/// get immediate feedback before a request is sent. The API enforces its own rules, so this layer
/// is a convenience and not a security boundary.
/// </summary>
/// <remarks>Every Validate method returns a user-facing error message, or null when valid.</remarks>
public static class UIValidator
{
    public const int AccountNameMaxLength      = 25;
    public const int DescriptionMaxLength      = 40;
    public const int CategoryMaxLength         = 25;
    public const int AmendDescriptionMaxLength = 40;
    public const int FeedbackBodyMinLength     = 10;
    public const int FeedbackBodyMaxLength     = 4000;

    // Example shown in every numeric error message.
    private const string NumericHint = "e.g. 100.00";

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
    /// Checks a proposed account name against the existing accounts, ignoring case. When editing,
    /// pass the edited account's ID so it can keep its own name.
    /// </summary>
    public static string? ValidateAccountNameUnique(
        string? value,
        IEnumerable<Models.Account> existingAccounts,
        Guid? currentAccountId = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return null; // ValidateAccountName reports empty names.

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
        // The category is optional.
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
            // A one-time transaction ends the day it starts.
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
        // The description is optional when amending.
        if (string.IsNullOrWhiteSpace(value)) return null;

        var trimmed = value.Trim();

        if (trimmed.Length > AmendDescriptionMaxLength)
            return $"Description cannot exceed {AmendDescriptionMaxLength} characters.";

        if (ContainsUnsafeContent(trimmed))
            return "Description contains invalid characters.";

        return null;
    }

    /// <summary>
    /// Checks a feedback message after trimming. Unlike names and descriptions it allows any
    /// punctuation, because bug reports often contain code, symbols and several lines.
    /// </summary>
    public static string? ValidateFeedbackBody(string? value)
    {
        var length = (value ?? string.Empty).Trim().Length;

        if (length == 0)
            return "Please describe your feedback.";

        if (length < FeedbackBodyMinLength)
            return $"Please write at least {FeedbackBodyMinLength} characters.";

        if (length > FeedbackBodyMaxLength)
            return $"Please keep your message to {FeedbackBodyMaxLength} characters or fewer.";

        return null;
    }

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

    /// <summary>
    /// Strips every character that is not a digit, a leading minus sign (when allowed) or a single
    /// decimal point, and limits the input to two decimal places. Cheap enough to call on every
    /// keystroke.
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
                    // Extra decimal places are dropped.
                }
                else
                {
                    sb.Append(ch);
                    leadingPos = false;
                }
            }
            // Every other character is dropped.
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detects HTML markup, SQL comment sequences, bare semicolons and common SQL statement
    /// patterns. This is defense in depth: the API remains the authoritative check.
    /// </summary>
    private static bool ContainsUnsafeContent(string input)
    {
        // HTML and script tags.
        if (input.Contains('<') || input.Contains('>'))
            return true;

        // SQL comment sequences.
        if (input.Contains("--") || input.Contains("/*") || input.Contains("*/"))
            return true;

        // A statement keyword directly after a quote or semicolon, for example "; DROP".
        if (Regex.IsMatch(input,
            @"[;'""`]\s*(DROP|DELETE|INSERT|UPDATE|SELECT|UNION|EXEC|EXECUTE|ALTER|CREATE|TRUNCATE)\b",
            RegexOptions.IgnoreCase))
            return true;

        // None of the text fields has a legitimate use for a semicolon.
        if (input.Contains(';'))
            return true;

        return false;
    }
}
