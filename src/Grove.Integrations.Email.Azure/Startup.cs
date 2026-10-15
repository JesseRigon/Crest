using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Integrations.Email.Azure.Models;
using Grove.Integrations.Email.Azure.Services;
using Grove.Communications.Email.Services;
using Grove.Environment.Shell.Configuration;
using Grove.Communications.Email;

namespace Grove.Integrations.Email.Azure;

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

        services.AddEmailProviderOptionsConfiguration<AzureEmailProviderOptionsConfigurations>();

        services.AddApiContributor<Api.AzureEmailSettingsApi>();

        services.Configure<DefaultAzureEmailOptions>(options =>
        {
            _shellConfiguration.GetSection("Grove_Email_AzureCommunicationServices").Bind(options);

            // The 'Grove_Email_Azure' key can be removed in version 3. 
            _shellConfiguration.GetSection("Grove_Email_Azure").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
