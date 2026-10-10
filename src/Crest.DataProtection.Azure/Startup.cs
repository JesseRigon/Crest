using Azure.Storage.Blobs;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;

namespace Crest.DataProtection.Azure;

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
        var connectionString = _configuration.GetValue<string>("Crest_DataProtection_Azure:ConnectionString");

        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Crest_DataProtection_Azure:ConnectionString was found. Adding 'Crest.DataProtection.Azure' feature services.");
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
            _logger.LogCritical("No connection string was supplied for Crest.DataProtection.Azure. Ensure that an application setting containing a valid Azure Storage connection string is available at `Crest:Crest_DataProtection_Azure:ConnectionString`.");
        }
    }
}
