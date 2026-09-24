using Jellyfin.Plugin.SeasonalContent.Collections;
using Jellyfin.Plugin.SeasonalContent.Jellyseerr;
using Jellyfin.Plugin.SeasonalContent.Lists;
using Jellyfin.Plugin.SeasonalContent.Lists.MdbList;
using Jellyfin.Plugin.SeasonalContent.Ownership;
using Jellyfin.Plugin.SeasonalContent.Playback;
using Jellyfin.Plugin.SeasonalContent.Setup;
using Jellyfin.Plugin.SeasonalContent.Stubs;
using Jellyfin.Plugin.SeasonalContent.Sync;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.SeasonalContent;

/// <summary>
/// Registers this plugin's services with Jellyfin's DI container. Services are added milestone
/// by milestone per docs/implementation-plan.md §3.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddSingleton<IMdbListApiClient, MdbListApiClient>();
        serviceCollection.AddSingleton<IListSource, MdbListSource>();
        serviceCollection.AddScoped<IMovieCatalog, LibraryMovieCatalog>();
        serviceCollection.AddScoped<IStubFileIoExecutor, StubFileIoExecutor>();
        serviceCollection.AddScoped<IStubLibraryScanner, StubLibraryScanner>();
        serviceCollection.AddScoped<ICollectionReconciler, CollectionReconciler>();
        serviceCollection.AddScoped<ILibrarySetupService, LibrarySetupService>();
        serviceCollection.AddScoped<IScheduledTask, SeasonalContentSyncTask>();
        serviceCollection.AddSingleton<IJellyseerrClient, JellyseerrClient>();
        serviceCollection.AddHostedService<StubPlaybackInterceptor>();
    }
}
