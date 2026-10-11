using Crest.Antivirus.ClamAV;
using Crest.BackgroundTasks;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Environment.Shell.Configuration;
using Crest.Media.Files;
using Crest.Media.Scanning.Detection;
using Crest.Media.Scanning.Endpoints;
using Crest.Media.Scanning.Indexes;
using Crest.Media.Scanning.Migrations;
using Crest.Media.Scanning.Scanners;
using Crest.Media.Scanning.Services;
using Crest.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crest.Media.Scanning;

/// <summary>
/// Upload scanning (docs/media.md § 2): the quarantine, the host's scanners and type detector, the
/// tenant's choice of its own scanner, the retry task and the offsite verdict callback. The file
/// store (the core Media module) registers the storage provider and the verdict handler.
/// </summary>
public sealed class Startup(IShellConfiguration configuration) : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<UploadScanningOptions>(configuration.GetSection(UploadScanningOptions.ConfigSection));
        services.Configure<ClamAvOptions>(configuration.GetSection(ClamAvOptions.ConfigSection));

        services.AddHttpClient(OffsiteUploadScanner.HttpClientName, client => client.Timeout = TimeSpan.FromMinutes(5));

        services.TryAddSingleton<ClamAvConnectionFactory>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IUploadScanner, ClamAvUploadScanner>());

        services.AddSingleton<SignatureFileTypeDetector>();
        services.AddSingleton<LibmagicFileTypeDetector>();
        services.AddSingleton<IFileTypeDetector>(FileTypeDetectors.Resolve);

        services.AddScoped<ITenantScanningSettings, SiteTenantScanningSettings>();
        services.AddScoped<ScannerCatalog>();
        services.AddScoped<IScanJobStore, YesSqlScanJobStore>();
        services.AddScoped<ScanCoordinator>();
        services.AddScoped<IUploadQuarantine, UploadQuarantine>();

        services.AddSingleton<IBackgroundTask, ScanJobBackgroundTask>();
        services.AddIndexProvider<ScanJobIndexProvider>();
        services.AddDataMigration<ScanJobMigrations>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.AddVerdictCallbackEndpoint();
    }
}
