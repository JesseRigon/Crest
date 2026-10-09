using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.Azure.Email.Drivers;
using Crest.DisplayManagement.Handlers;
using Crest.Email.Azure.Models;
using Crest.Email.Azure.Services;
using Crest.Email.Services;
using Crest.Environment.Shell.Configuration;

namespace Crest.Email.Azure;

public sealed class Startup
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddSignalOptionsChangeTokenSource<AzureEmailOptions>();

        services.AddTransient<IConfigureOptions<AzureEmailOptions>, AzureEmailOptionsConfiguration>();

        services.AddEmailProviderOptionsConfiguration<AzureEmailProviderOptionsConfigurations>()
            .AddSiteDisplayDriver<AzureEmailSettingsDisplayDriver>();

        services.Configure<DefaultAzureEmailOptions>(options =>
        {
            _shellConfiguration.GetSection("Crest_Email_AzureCommunicationServices").Bind(options);

            // The 'Crest_Email_Azure' key can be removed in version 3. 
            _shellConfiguration.GetSection("Crest_Email_Azure").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
