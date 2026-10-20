namespace HexFund.UI.Models;

/// <summary>
/// A single release entry in the changelog, loaded from Resources/changelog.json, which ships
/// inside the app package.
/// </summary>
public class PatchNote
{
    public string   Version { get; set; } = string.Empty;
    public string   Date    { get; set; } = string.Empty;
    public string[] Changes { get; set; } = Array.Empty<string>();
}
