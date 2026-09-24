using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.SeasonalContent;

/// <summary>
/// Registers this plugin's services with Jellyfin's DI container. Empty at M0;
/// services are added milestone by milestone per docs/implementation-plan.md §3.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
    }
}
