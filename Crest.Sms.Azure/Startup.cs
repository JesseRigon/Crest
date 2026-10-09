using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crest.DisplayManagement.Handlers;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;
using Crest.Sms.Azure.Drivers;
using Crest.Sms.Azure.Models;

namespace Crest.Sms.Azure;

public sealed class Startup : StartupBase
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAzureSmsProvider()
            .AddSiteDisplayDriver<AzureSettingsDisplayDriver>();

        services.Configure<DefaultAzureSmsOptions>(options =>
        {
            _shellConfiguration.GetSection("Crest_Sms_AzureCommunicationServices").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
