using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Email;

public sealed class AzureEmailOptionsConfiguration(ISiteService siteService) : IConfigureOptions<AzureEmailOptions>
{
    public void Configure(AzureEmailOptions options)
    {
        var settings = siteService.GetSettings<AzureEmailSettings>();

        options.IsEnabled = settings.IsEnabled;
        options.DefaultSender = settings.DefaultSender;
        options.ConnectionKey = string.IsNullOrEmpty(settings.ConnectionKey) ? AzureCommunicationServices.EmailConnectionKey : settings.ConnectionKey;
    }
}
