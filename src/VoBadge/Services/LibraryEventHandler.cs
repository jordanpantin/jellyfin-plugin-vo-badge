using System.Collections.Concurrent;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.VoBadge.Services;

/// <summary>
/// Handles library item added/updated events to manage VO badges.
/// </summary>
public class LibraryEventHandler : IHostedService, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ItemBadgeProcessor _itemBadgeProcessor;
    private readonly ILogger<LibraryEventHandler> _logger;
    private readonly ConcurrentDictionary<Guid, byte> _inProgress = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryEventHandler"/> class.
    /// </summary>
    /// <param name="libraryManager">Library manager.</param>
    /// <param name="itemBadgeProcessor">Item badge processor.</param>
    /// <param name="logger">Logger.</param>
    public LibraryEventHandler(
        ILibraryManager libraryManager,
        ItemBadgeProcessor itemBadgeProcessor,
        ILogger<LibraryEventHandler> logger)
    {
        _libraryManager = libraryManager;
        _itemBadgeProcessor = itemBadgeProcessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded += OnItemChanged;
        _libraryManager.ItemUpdated += OnItemChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _libraryManager.ItemAdded -= OnItemChanged;
        _libraryManager.ItemUpdated -= OnItemChanged;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Disposes managed resources.
    /// </summary>
    /// <param name="disposing">True if disposing.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _libraryManager.ItemAdded -= OnItemChanged;
            _libraryManager.ItemUpdated -= OnItemChanged;
        }
    }

    private void OnItemChanged(object? sender, ItemChangeEventArgs e)
    {
        var item = e.Item;
        if (item is null || !_inProgress.TryAdd(item.Id, 0))
        {
            return;
        }

        _ = ProcessItemAsync(item);
    }

    private async Task ProcessItemAsync(BaseItem item)
    {
        try
        {
            await _itemBadgeProcessor.ProcessItemAsync(item, reapplyExisting: false, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing item change for: {Name}", item.Name);
        }
        finally
        {
            _inProgress.TryRemove(item.Id, out _);
        }
    }
}
