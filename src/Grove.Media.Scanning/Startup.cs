using Grove.Antivirus.ClamAV;
using Grove.BackgroundTasks;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Environment.Shell.Configuration;
using Grove.Media.Files;
using Grove.Media.Scanning.Detection;
using Grove.Media.Scanning.Endpoints;
using Grove.Media.Scanning.Indexes;
using Grove.Media.Scanning.Migrations;
using Grove.Media.Scanning.Scanners;
using Grove.Media.Scanning.Services;
using Grove.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Grove.Media.Scanning;

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
