#nullable enable
using Crest.Data;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crest.Media.Files;

public static class FileServiceCollectionExtensions
{
    /// <summary>The file-object store, graph, service, local storage and URL signer.</summary>
    public static IServiceCollection AddFileObjects(this IServiceCollection services)
    {
        services.AddScoped<IMediaObjectStore, YesSqlMediaObjectStore>();
        services.AddScoped<MediaGraph>();
        services.AddScoped<IMediaGraph>(serviceProvider => serviceProvider.GetRequiredService<MediaGraph>());
        services.AddScoped<MediaObjectService>();
        services.AddScoped<IMediaObjectService>(serviceProvider => serviceProvider.GetRequiredService<MediaObjectService>());
        services.AddScoped<IMediaObjectWrites>(serviceProvider => serviceProvider.GetRequiredService<MediaObjectService>());
        services.AddScoped<IMediaVerdictHandler>(serviceProvider => serviceProvider.GetRequiredService<MediaObjectService>());
        services.AddScoped<IMediaObjectListing>(serviceProvider => serviceProvider.GetRequiredService<MediaObjectService>());
        services.AddScoped<IMediaDriveAdministration>(serviceProvider => serviceProvider.GetRequiredService<MediaObjectService>());
        services.AddScoped<IPersonalDriveAccessStore, YesSqlPersonalDriveAccessStore>();
        services.AddScoped<PersonalDriveAccessService>();
        services.AddScoped<IPersonalDriveAccessService>(serviceProvider => serviceProvider.GetRequiredService<PersonalDriveAccessService>());
        services.AddScoped<IPersonalDriveNotifier, NotificationPersonalDriveNotifier>();
        services.AddScoped<IFileDriveSettingsAccessor, SiteFileDriveSettingsAccessor>();

        services.AddSingleton(serviceProvider => new LocalMediaStorageProvider(FilesRoot(serviceProvider)));
        services.AddSingleton<IMediaStorageProvider>(serviceProvider => serviceProvider.GetRequiredService<LocalMediaStorageProvider>());

        services.AddSingleton(serviceProvider => new HmacMediaUrlSigner(HmacMediaUrlSigner.ResolveKey(
            serviceProvider.GetRequiredService<IShellConfiguration>()["Crest_Media:Files:UrlSigningKey"],
            FilesRoot(serviceProvider),
            serviceProvider.GetRequiredService<IDataProtectionProvider>())));
        services.AddSingleton<IMediaUrlSigner>(serviceProvider => serviceProvider.GetRequiredService<HmacMediaUrlSigner>());

        services.AddIndexProvider<MediaContentIndexProvider>();
        services.AddIndexProvider<MediaPlacementIndexProvider>();
        services.AddIndexProvider<PersonalDriveAccessIndexProvider>();
        services.AddDataMigration<FileMigrations>();
        return services;
    }

    /// <summary>The tenant's file root, <c>App_Data/Sites/&lt;tenant&gt;/Files</c>, beside the media library's <c>Media</c>.</summary>
    private static string FilesRoot(IServiceProvider serviceProvider)
    {
        var shellOptions = serviceProvider.GetRequiredService<IOptions<ShellOptions>>().Value;
        var shellSettings = serviceProvider.GetRequiredService<ShellSettings>();
        return Path.GetFullPath(Path.Combine(shellOptions.ShellsApplicationDataPath, shellOptions.ShellsContainerName, shellSettings.Name, "Files"));
    }
}
