using System.Text.Json;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.VoBadge.Configuration;

/// <summary>
/// Plugin configuration.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    private static readonly JsonSerializerOptions BadgeJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="PluginConfiguration"/> class.
    /// </summary>
    public PluginConfiguration()
    {
        EnableForMovies = true;
        EnableForEpisodes = true;
        FrenchLanguageCodes = "fre,fra,fr,french,français,francais";
        ClearMarkers = string.Empty;
    }

    /// <summary>
    /// Gets or sets the badges the user can edit and extend.
    /// Must stay empty here. Jellyfin's XML serializer appends deserialized items
    /// onto any list created in the constructor, so seeding defaults restored removed badges after every restart.
    /// </summary>
    public List<BadgeDefinition> Badges { get; set; } = [];

    /// <summary>
    /// Gets or sets the same badges as JSON. This is the list the config page saved.
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
    /// Gets the badges to apply. Defaults are used only when nothing has been saved.
    /// </summary>
    /// <returns>Enabled badges. Empty when the user disabled or removed every badge.</returns>
    public List<BadgeDefinition> GetBadges()
    {
        return ReadBadges()
            .Where(b => b.Enabled && !string.IsNullOrWhiteSpace(b.Text))
            .ToList();
    }

    /// <summary>
    /// Copies the saved JSON list onto <see cref="Badges"/> so the API does not return a stale XML list.
    /// </summary>
    public void ApplySavedBadges()
    {
        var saved = ReadSavedBadges();
        if (saved is not null)
        {
            Badges = saved;
        }
    }

    private List<BadgeDefinition> ReadBadges()
    {
        return ReadSavedBadges() ?? BadgeDefinition.CreateDefaults();
    }

    /// <summary>
    /// Returns the list the user saved, or null when badges were never saved.
    /// An empty list is kept: it means every badge was removed.
    /// </summary>
    /// <returns>The saved badges, or null.</returns>
    private List<BadgeDefinition>? ReadSavedBadges()
    {
        if (!string.IsNullOrWhiteSpace(BadgesJson))
        {
            try
            {
                return JsonSerializer.Deserialize<List<BadgeDefinition>>(BadgesJson, BadgeJsonOptions) ?? [];
            }
            catch (JsonException)
            {
                // Fall through to the XML list.
            }
        }

        if (Badges is { Count: > 0 })
        {
            return Badges;
        }

        return null;
    }
}
