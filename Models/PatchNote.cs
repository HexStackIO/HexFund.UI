namespace HexFund.UI.Models;

/// <summary>
/// Represents a single release entry in the changelog.
/// Populated from Resources/changelog.json which ships with the app binary.
/// To add a new release: prepend an entry to that file before committing.
/// </summary>
public class PatchNote
{
    public string   Version { get; set; } = string.Empty;
    public string   Date    { get; set; } = string.Empty;
    public string[] Changes { get; set; } = Array.Empty<string>();
}
