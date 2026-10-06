using System;
using Jellyfin.Plugin.VoBadge.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.VoBadge;

/// <summary>
/// VoBadge Plugin – adds a "VO" badge on posters when French audio is missing.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Plugin"/> class.
    /// </summary>
    /// <param name="applicationPaths">Application paths.</param>
    /// <param name="xmlSerializer">XML serializer.</param>
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Gets the plugin instance.
    /// </summary>
    public static Plugin? Instance { get; private set; }

    /// <inheritdoc />
    public override string Name => "VO Badge";

    /// <inheritdoc />
    public override Guid Id => new("217e21a6-08a7-4495-be64-a3cd6a7fa200");

    /// <inheritdoc />
    public override string Description => "Adds a VO badge on movie/episode posters when French audio track is missing.";

    /// <inheritdoc />
    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.configPage.html",
                EnableInMainMenu = true
            },
            new PluginPageInfo
            {
                Name = "VoBadgeConfig.js",
                EmbeddedResourcePath = $"{GetType().Namespace}.Configuration.VoBadgeConfig.js"
            }
        };
    }
}
