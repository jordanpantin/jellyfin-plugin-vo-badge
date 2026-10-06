using Jellyfin.Plugin.VoBadge.Services;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.VoBadge;

/// <summary>
/// Registers plugin services with the DI container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<BadgeOverlayService>();
        serviceCollection.AddSingleton<ItemBadgeProcessor>();
        serviceCollection.AddHostedService<LibraryEventHandler>();
    }
}
