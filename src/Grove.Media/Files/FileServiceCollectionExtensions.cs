#nullable enable
using Grove.Data;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Media.Files;

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
        services.AddScoped<IMediaDriveProvisioning, MediaDriveProvisioner>();
        services.AddScoped<IMediaGeneratedFiles, MediaGeneratedFiles>();
        services.AddOptions<MediaScanExemptionOptions>()
            .Configure<IShellConfiguration>((options, configuration) =>
            {
                foreach (var exemption in configuration.GetSection("Grove_Media:Generated:Exemptions").Get<List<MediaScanExemption>>() ?? [])
                {
                    options.Exemptions.Add(exemption);
                }
            });
        services.AddScoped<Grove.Access.Users.Handlers.IUserEventHandler, PersonalDriveUserEventHandler>();

        services.AddSingleton(serviceProvider => new LocalMediaStorageProvider(FilesRoot(serviceProvider)));
        services.AddSingleton<IMediaStorageProvider>(serviceProvider => serviceProvider.GetRequiredService<LocalMediaStorageProvider>());

        services.AddSingleton(serviceProvider => new HmacMediaUrlSigner(HmacMediaUrlSigner.ResolveKey(
            serviceProvider.GetRequiredService<IShellConfiguration>()["Grove_Media:Files:UrlSigningKey"],
            FilesRoot(serviceProvider),
            serviceProvider.GetRequiredService<IDataProtectionProvider>())));

        // The private CDN zone's signer (docs/media.md › Serving and CDNs): private URLs are
        // signed for the zone when one is configured, and the origin accepts its tokens too.
        services.AddSingleton(serviceProvider => new MediaUrlSigners(
            serviceProvider.GetRequiredService<HmacMediaUrlSigner>(),
            CdnUrlSigners.Create(serviceProvider.GetRequiredService<IShellConfiguration>().GetSection(MediaCdnOptions.Section).Get<MediaCdnOptions>() ?? new MediaCdnOptions())));
        services.AddSingleton<IMediaUrlSigner>(serviceProvider => serviceProvider.GetRequiredService<MediaUrlSigners>().Active);

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
