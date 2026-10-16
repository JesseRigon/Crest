using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Microsoft.Sms;

public sealed class AzureSmsOptionsConfiguration(ISiteService siteService) : IConfigureOptions<AzureSmsOptions>
{
    public void Configure(AzureSmsOptions options)
    {
        var settings = siteService.GetSettings<AzureSmsSettings>();

        options.IsEnabled = settings.IsEnabled;
        options.PhoneNumber = settings.PhoneNumber;
        options.ConnectionKey = string.IsNullOrEmpty(settings.ConnectionKey) ? AzureCommunicationServices.SmsConnectionKey : settings.ConnectionKey;
    }
}
