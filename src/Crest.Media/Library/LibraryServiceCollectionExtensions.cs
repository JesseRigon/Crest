#nullable enable
using Crest.Access;
using Crest.ContentManagement.Handlers;
using Crest.Environment.Shell.Configuration;
using Crest.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crest.Media.Library;

public static class LibraryServiceCollectionExtensions
{
    /// <summary>The media library (ruling F1): <c>/media</c> paths over the library drive, media
    /// references and URLs, the import and its recipe step.</summary>
    public static IServiceCollection AddMediaLibrary(this IServiceCollection services)
    {
        services.TryAddScoped<IMediaLibraryState, DocumentMediaLibraryState>();
        services.TryAddScoped<IPlacementNameLookup, YesSqlPlacementNameLookup>();
        services.AddScoped<IMediaLibrary, MediaLibrary>();
        services.AddScoped<IMediaReferenceResolver, MediaReferenceResolver>();
        services.AddScoped<IResourcePermissionMapper, LibraryPlacementPermissionMapper>();
        services.AddScoped<MediaLibrarySharingImport>();
        services.AddScoped<IMediaFieldRewriter, MediaFieldRewriter>();
        services.AddScoped<IContentHandler, MediaFieldImportHandler>();
        services.AddScoped<MediaLibraryImporter>();
        services.AddTransient<MediaLibraryRequestMiddleware>();
        services.AddRecipeExecutionStep<MediaLibraryImportStep>();

        services.AddOptions<MediaLibraryOptions>()
            .Configure<IShellConfiguration>((options, configuration) =>
            {
                if (bool.TryParse(configuration["Crest_Media:Library:TrustExistingFiles"], out var trust))
                {
                    options.TrustExistingFiles = trust;
                }

                if (int.TryParse(configuration["Crest_Media:Library:SignedUrlMinutes"], out var minutes) && minutes > 0)
                {
                    options.SignedUrlLifetime = TimeSpan.FromMinutes(minutes);
                }
            });

        return services;
    }
}
