using Jellyfin.Data.Enums;
using Jellyfin.Plugin.VoBadge.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VoBadge.Services;

/// <summary>
/// Scheduled task that scans all movies and episodes to add/remove VO badges.
/// </summary>
public class VoBadgeScanTask : IScheduledTask
{
    private readonly ILibraryManager _libraryManager;
    private readonly ItemBadgeProcessor _itemBadgeProcessor;
    private readonly ILogger<VoBadgeScanTask> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoBadgeScanTask"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="itemBadgeProcessor">Item badge processor.</param>
    /// <param name="logger">Logger.</param>
    public VoBadgeScanTask(
        ILibraryManager libraryManager,
        ItemBadgeProcessor itemBadgeProcessor,
        ILogger<VoBadgeScanTask> logger)
    {
        _libraryManager = libraryManager;
        _itemBadgeProcessor = itemBadgeProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public string Name => "VO Badge - Scan Library";

    /// <inheritdoc />
    public string Key => "VoBadgeScanTask";

    /// <inheritdoc />
    public string Description => "Scans all movies and episodes to add or remove the VO badge based on available audio tracks.";

    /// <inheritdoc />
    public string Category => "VO Badge";

    /// <inheritdoc />
    public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
    {
        return new[]
        {
            new TaskTriggerInfo
            {
                Type = TaskTriggerInfoType.DailyTrigger,
                TimeOfDayTicks = TimeSpan.FromHours(3).Ticks
            }
        };
    }

    /// <inheritdoc />
    public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var config = Plugin.Instance?.Configuration ?? new PluginConfiguration();
        var items = new List<BaseItem>();

        if (config.EnableForMovies)
        {
            items.AddRange(_libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Movie },
                Recursive = true
            }));
        }

        if (config.EnableForEpisodes)
        {
            items.AddRange(_libraryManager.GetItemList(new InternalItemsQuery
            {
                IncludeItemTypes = new[] { BaseItemKind.Episode },
                Recursive = true
            }));
        }

        _logger.LogInformation("VO Badge scan starting. Processing {Count} items.", items.Count);

        for (var i = 0; i < items.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            progress.Report((double)i / items.Count * 100);

            try
            {
                await _itemBadgeProcessor.ProcessItemAsync(items[i], reapplyExisting: true, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing item: {Name}", items[i].Name);
            }
        }

        progress.Report(100);
        _logger.LogInformation("VO Badge scan completed.");
    }
}
