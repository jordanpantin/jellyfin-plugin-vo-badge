using System.Text.RegularExpressions;
using Jellyfin.Plugin.VoBadge.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.VoBadge.Services;

/// <summary>
/// Picks a configured badge from audio tracks and the release name.
/// </summary>
public static class AudioLanguageDetector
{
    /// <summary>
    /// Resolves the badge for an item.
    /// </summary>
    /// <param name="item">Library item.</param>
    /// <param name="config">Plugin configuration.</param>
    /// <returns>Null when streams are not probed. Badge is null when no overlay should be shown.</returns>
    public static BadgeMatch? Match(BaseItem item, PluginConfiguration config)
    {
        var mediaStreams = item.GetMediaStreams();
        if (mediaStreams is null)
        {
            return null;
        }

        var audioStreams = mediaStreams.Where(s => s.Type == MediaStreamType.Audio).ToList();
        if (audioStreams.Count == 0)
        {
            return null;
        }

        var badges = config.GetBadges();
        var clearMarkers = ParseMarkers(config.ClearMarkers);
        var frenchCodes = ParseMarkers(config.FrenchLanguageCodes);
        var location = $"{item.Name} {item.Path}";

        var sawClear = ContainsAny(location, clearMarkers);
        var sawGenericFrench = false;
        var texts = new List<(string? Language, string Title)>();

        foreach (var stream in audioStreams)
        {
            var language = stream.Language;
            var title = $"{stream.Title} {stream.DisplayTitle}";
            texts.Add((language, title));

            if (Matches(language, title, clearMarkers))
            {
                sawClear = true;
            }

            if (Matches(language, title, frenchCodes))
            {
                sawGenericFrench = true;
            }
        }

        if (sawClear)
        {
            return new BadgeMatch(null);
        }

        foreach (var badge in badges.Where(b => !string.IsNullOrWhiteSpace(b.Markers)))
        {
            var markers = ParseMarkers(badge.Markers);
            if (texts.Any(t => Matches(t.Language, t.Title, markers)))
            {
                return new BadgeMatch(badge);
            }

            if (sawGenericFrench && ContainsAny(location, markers))
            {
                return new BadgeMatch(badge);
            }
        }

        if (sawGenericFrench)
        {
            return new BadgeMatch(null);
        }

        var fallback = badges.FirstOrDefault(b => string.IsNullOrWhiteSpace(b.Markers));
        return new BadgeMatch(fallback);
    }

    private static List<string> ParseMarkers(string? codes)
    {
        return (codes ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(c => c.ToLowerInvariant())
            .Where(c => c.Length > 0)
            .Distinct()
            .ToList();
    }

    private static bool Matches(string? language, string? title, IEnumerable<string> markers)
    {
        foreach (var marker in markers)
        {
            if (IsLanguageTag(marker))
            {
                if (LanguageEquals(language, marker))
                {
                    return true;
                }

                continue;
            }

            if (ContainsWord(title, marker) || ContainsWord(language, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAny(string? text, IEnumerable<string> markers)
    {
        foreach (var marker in markers)
        {
            if (!IsLanguageTag(marker) && ContainsWord(text, marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsLanguageTag(string marker)
    {
        return Regex.IsMatch(marker, @"^(fr|fra|fre)(-[a-z]{2})?$", RegexOptions.CultureInvariant);
    }

    private static bool LanguageEquals(string? language, string marker)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return false;
        }

        var lang = language.Trim().ToLowerInvariant().Replace('_', '-');
        return lang == marker || lang.StartsWith(marker + "-", StringComparison.Ordinal);
    }

    private static bool ContainsWord(string? text, string marker)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return Regex.IsMatch(text, $@"\b{Regex.Escape(marker)}\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}

/// <summary>
/// Result of a probed item.
/// </summary>
/// <param name="Badge">Badge to draw, or null to remove any overlay.</param>
public readonly record struct BadgeMatch(BadgeDefinition? Badge);
