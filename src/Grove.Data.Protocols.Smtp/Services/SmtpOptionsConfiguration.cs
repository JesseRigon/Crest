using Grove.Infrastructure.Settings;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp.Services;

/// <summary>Reads the tenant's SMTP provider options from <see cref="SmtpSettings"/>.</summary>
public sealed class SmtpOptionsConfiguration(ISiteService siteService) : IConfigureOptions<SmtpOptions>
{
    public void Configure(SmtpOptions options)
    {
        var settings = siteService.GetSettings<SmtpSettings>();

        options.DefaultSender = settings.DefaultSender;
        options.ConnectionKey = settings.ConnectionKey;
        options.IsEnabled = settings.IsEnabled && !string.IsNullOrEmpty(settings.ConnectionKey);
    }
}
