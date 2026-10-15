using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.DisplayManagement.Handlers;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Sms.Azure.Drivers;
using Grove.Sms.Azure.Models;

namespace Grove.Sms.Azure;

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
            _shellConfiguration.GetSection("Grove_Sms_AzureCommunicationServices").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
