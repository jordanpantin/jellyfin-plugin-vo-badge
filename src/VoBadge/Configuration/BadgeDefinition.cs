namespace Jellyfin.Plugin.VoBadge.Configuration;

/// <summary>
/// A user-defined badge. Empty markers mean the fallback badge, used when no rule matches.
/// </summary>
public class BadgeDefinition
{
    /// <summary>
    /// Gets or sets the text drawn on the pill.
    /// </summary>
    public string Text { get; set; } = "VO";

    /// <summary>
    /// Gets or sets the accent or fill color.
    /// </summary>
    public string ColorHex { get; set; } = "#C4A35A";

    /// <summary>
    /// Gets or sets the text color.
    /// </summary>
    public string TextColorHex { get; set; } = "#F6F1E7";

    /// <summary>
    /// Gets or sets the relative size. 18 is the default pill.
    /// </summary>
    public int SizePercent { get; set; } = 18;

    /// <summary>
    /// Gets or sets comma-separated markers. Empty means fallback badge.
    /// </summary>
    public string Markers { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the pill is filled with <see cref="ColorHex"/>.
    /// </summary>
    public bool Filled { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this badge is used.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Builds the default VO and VF badges.
    /// </summary>
    /// <returns>The default badges.</returns>
    public static List<BadgeDefinition> CreateDefaults()
    {
        return
        [
            new BadgeDefinition
            {
                Text = "VO",
                ColorHex = "#C4A35A",
                TextColorHex = "#F6F1E7",
                SizePercent = 18,
                Markers = string.Empty,
                Filled = false,
                Enabled = true
            },
            new BadgeDefinition
            {
                Text = "VF",
                ColorHex = "#1F7A4D",
                TextColorHex = "#F7F8FA",
                SizePercent = 18,
                Markers = "vf,vff,vfi,fr-fr,fra-fr,fre-fr,truefrench,true french,fra,fre,fr,french,français,francais",
                Filled = true,
                Enabled = true
            }
        ];
    }
}
