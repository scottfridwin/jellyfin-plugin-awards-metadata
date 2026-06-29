namespace Jellyfin.Plugin.AwardsMetadata.Scraper.Models;

/// <summary>
/// Represents whether a nomination was a winner or just a nominee.
/// </summary>
public enum AwardResult
{
    /// <summary>
    /// The nomination did not win.
    /// </summary>
    Nominee = 0,

    /// <summary>
    /// The nomination won the award.
    /// </summary>
    Winner = 1,
}
