using System.Text.Json;

namespace HexFund.UI.Services.Http;

/// <summary>The fields of an RFC 7807 problem details body that the app uses.</summary>
public sealed record ProblemInfo(
    string? Title,
    string? Detail,
    int? Status,
    IReadOnlyDictionary<string, string[]> Errors,
    string? CorrelationId)
{
    /// <summary>The first validation message for a field, or null. Field names ignore case.</summary>
    public string? FirstError(string field)
    {
        foreach (var (key, messages) in Errors)
            if (string.Equals(key, field, StringComparison.OrdinalIgnoreCase) && messages.Length > 0)
                return messages[0];
        return null;
    }
}

/// <summary>Reads an RFC 7807 body without ever throwing.</summary>
public static class ProblemDetailsParser
{
    /// <summary>Returns null when the body is empty, not JSON, or not a JSON object.</summary>
    public static ProblemInfo? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;

            int? status = null;
            if (root.TryGetProperty("status", out var s) && s.ValueKind == JsonValueKind.Number
                && s.TryGetInt32(out var statusValue))
                status = statusValue;

            return new ProblemInfo(
                GetString(root, "title"),
                GetString(root, "detail"),
                status,
                ParseErrors(root),
                GetString(root, "correlationId"));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static Dictionary<string, string[]> ParseErrors(JsonElement root)
    {
        var result = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return result;

        foreach (var field in errors.EnumerateObject())
        {
            var messages = new List<string>();
            if (field.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in field.Value.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text)
                        messages.Add(text);
            }
            else if (field.Value.ValueKind == JsonValueKind.String && field.Value.GetString() is { } single)
            {
                messages.Add(single);
            }
            result[field.Name] = messages.ToArray();
        }
        return result;
    }
}
