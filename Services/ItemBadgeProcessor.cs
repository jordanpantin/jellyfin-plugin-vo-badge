using Jellyfin.Plugin.VoBadge.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VoBadge.Services;

/// <summary>
/// Applies or removes the VO badge on an item's primary image.
/// </summary>
public class ItemBadgeProcessor
{
    private readonly BadgeOverlayService _badgeOverlayService;
    private readonly ILogger<ItemBadgeProcessor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemBadgeProcessor"/> class.
    /// </summary>
    /// <param name="badgeOverlayService">Badge overlay service.</param>
    /// <param name="logger">Logger.</param>
    public ItemBadgeProcessor(BadgeOverlayService badgeOverlayService, ILogger<ItemBadgeProcessor> logger)
    {
        _badgeOverlayService = badgeOverlayService;
        _logger = logger;
    }

    /// <summary>
    /// Processes a single library item.
    /// </summary>
    /// <param name="item">Library item.</param>
    /// <param name="reapplyExisting">True to redraw badges already present (e.g. after config change).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the operation.</returns>
    public async Task ProcessItemAsync(BaseItem item, bool reapplyExisting, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        if (!ShouldProcess(item, config))
        {
            return;
        }

        var imageInfo = item.GetImageInfo(ImageType.Primary, 0);
        if (imageInfo is null || !imageInfo.IsLocalFile || string.IsNullOrEmpty(imageInfo.Path))
        {
            return;
        }

        var match = AudioLanguageDetector.Match(item, config);
        if (match is null)
        {
            return;
        }

        var badge = match.Value.Badge;
        var changed = false;
        var hasBadge = _badgeOverlayService.HasBadge(imageInfo.Path);

        if (badge is null)
        {
            if (hasBadge)
            {
                changed = _badgeOverlayService.RemoveBadge(imageInfo.Path);
                if (changed)
                {
                    _logger.LogInformation("Removed badge from: {Name}", item.Name);
                }
            }
        }
        else if (reapplyExisting || !hasBadge || !string.Equals(_badgeOverlayService.GetBadgeLabel(imageInfo.Path), badge.Text, StringComparison.Ordinal))
        {
            changed = _badgeOverlayService.AddBadge(
                imageInfo.Path,
                badge.Text,
                badge.ColorHex,
                badge.TextColorHex,
                badge.SizePercent,
                badge.Filled);
            if (changed)
            {
                _logger.LogInformation("Added {Label} badge to: {Name}", badge.Text, item.Name);
            }
        }

        if (!changed)
        {
            return;
        }

        imageInfo.DateModified = DateTime.UtcNow;
        imageInfo.Width = 0;
        imageInfo.Height = 0;
        await item.UpdateToRepositoryAsync(ItemUpdateType.ImageUpdate, cancellationToken).ConfigureAwait(false);
    }

    private static bool ShouldProcess(BaseItem item, PluginConfiguration config)
    {
        if (item.IsVirtualItem)
        {
            return false;
        }

        return item switch
        {
            Movie => config.EnableForMovies,
            Episode => config.EnableForEpisodes,
            _ => false
        };
    }
}
