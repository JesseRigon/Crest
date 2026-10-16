using Grove.Infrastructure.Settings;
using Grove.Integrations.Twilio.Models;
using Microsoft.Extensions.Options;

namespace Grove.Integrations.Twilio.Services;

public sealed class TwilioOptionsConfiguration(ISiteService siteService) : IConfigureOptions<TwilioOptions>
{
    public void Configure(TwilioOptions options)
    {
        var settings = siteService.GetSettings<TwilioSettings>();

        options.IsEnabled = settings.IsEnabled;
        options.PhoneNumber = settings.PhoneNumber;
        options.AccountSID = settings.AccountSID;
        options.ConnectionKey = string.IsNullOrEmpty(settings.ConnectionKey) ? TwilioConnection.Key : settings.ConnectionKey;
    }
}
