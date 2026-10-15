using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Grove.BackgroundTasks;
using Grove.Media.Services;

namespace Grove.Media;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the chunked file upload service and its background cleanup task.
    /// This method is safe to call multiple times; services are only registered once.
    /// </summary>
    public static IServiceCollection AddChunkFileUploadServices(this IServiceCollection services)
    {
        services.TryAddSingleton<IChunkFileUploadService, ChunkFileUploadService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, ChunkFileUploadBackgroundTask>());

        return services;
    }
}
