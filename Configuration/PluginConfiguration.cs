using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.VoBadge.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        Badges = BadgeDefinition.CreateDefaults();
        EnableForMovies = true;
        EnableForEpisodes = true;
        FrenchLanguageCodes = "fre,fra,fr,french,français,francais";
        ClearMarkers = string.Empty;
    }

    /// <summary>
    /// Gets or sets the badges the user can edit and extend.
    /// </summary>
    public List<BadgeDefinition> Badges { get; set; }

    /// <summary>
    /// Gets or sets the same badges as JSON, so the list survives XML save.
    /// </summary>
    public string BadgesJson { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether badge is enabled for movies.
    /// </summary>
    public bool EnableForMovies { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether badge is enabled for episodes.
    /// </summary>
    public bool EnableForEpisodes { get; set; } = true;

    /// <summary>
    /// Gets or sets language codes treated as generic French.
    /// </summary>
    public string FrenchLanguageCodes { get; set; } = "fre,fra,fr,french,français,francais";

    /// <summary>
    /// Gets or sets markers that suppress every badge.
    /// </summary>
    public string ClearMarkers { get; set; } = string.Empty;

    /// <summary>
    /// Gets the badges to apply, falling back to the VO and VFQ examples.
    /// </summary>
    /// <returns>Enabled badges.</returns>
    public List<BadgeDefinition> GetBadges()
    {
        var badges = ReadBadges()
            .Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Text))
            .ToList();

        return badges.Count > 0 ? badges : BadgeDefinition.CreateDefaults();
    }

    private List<BadgeDefinition> ReadBadges()
    {
        if (Badges is { Count: > 0 })
        {
            return Badges;
        }

        if (string.IsNullOrWhiteSpace(BadgesJson))
        {
            return [];
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<BadgeDefinition>>(BadgesJson) ?? [];
        }
        catch (System.Text.Json.JsonException)
        {
            return [];
        }
    }
}
