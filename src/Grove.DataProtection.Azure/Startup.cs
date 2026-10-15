using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;

namespace Grove.DataProtection.Azure;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _configuration;
    private readonly ILogger _logger;

    public Startup(IShellConfiguration configuration, ILogger<Startup> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public override int Order
        => PlatformConstants.ConfigureOrder.AzureDataProtection;

    public override void ConfigureServices(IServiceCollection services)
    {
        var connectionString = _configuration.GetValue<string>("Grove_DataProtection_Azure:ConnectionString");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Grove_DataProtection_Azure:ConnectionString was found. Adding 'Grove.DataProtection.Azure' feature services.");
            }

            // Remove any previously registered options setups.
            services.RemoveAll<IConfigureOptions<KeyManagementOptions>>();

            services
                .AddDataProtection()
                .PersistKeysToAzureBlobStorage(sp =>
                {
                    var options = sp.GetRequiredService<IOptions<BlobOptions>>().Value;

                    var logger = sp.GetRequiredService<ILogger<Startup>>();

                    if (logger.IsEnabled(LogLevel.Debug))
                    {
                        logger.LogDebug("Creating BlobClient instance using '{ContainerName}' as container name and '{BlobName}' as blob name.", options.ContainerName, options.BlobName);
                    }

                    return new BlobClient(
                        options.ConnectionString,
                        options.ContainerName,
                        options.BlobName);
                });

            services.AddSingleton<IConfigureOptions<BlobOptions>, BlobOptionsConfiguration>();

            services.AddScoped<IModularTenantEvents, BlobModularTenantEvents>();
        }
        else
        {
            // Enabled without a connection string is an expected state (a tenant enables every
            // feature in dev): nothing is registered, so the tenant keeps its default key ring.
            _logger.LogWarning("Grove.DataProtection.Azure is enabled but not active because 'Grove_DataProtection_Azure:ConnectionString' is not configured; the tenant keeps its default key storage.");
        }
    }
}
