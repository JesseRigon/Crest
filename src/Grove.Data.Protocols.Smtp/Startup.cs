using Grove.Communications.Email;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Environment.Options;
using Grove.Environment.Shell.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Grove.Data.Protocols.Smtp;

public sealed class Startup
{
    private readonly IShellConfiguration _shellConfiguration;

    public Startup(IShellConfiguration shellConfiguration)
    {
        _shellConfiguration = shellConfiguration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        // The protocol: the connector engine runs every smtp connection through it.
        services.AddConnectorProtocol<SmtpConnectorProtocol>();

        // The e-mail providers: the tenant's SMTP provider over a connection, and the host's
        // pickup-directory delivery (development and test hosts).
        services.AddEmailProviderOptionsConfiguration<SmtpProviderOptionsConfigurations>()
            .AddSignalOptionsChangeTokenSource<SmtpOptions>()
            .AddTransient<IConfigureOptions<SmtpOptions>, SmtpOptionsConfiguration>()
            .AddTransient<IPostConfigureOptions<DefaultSmtpOptions>, DefaultSmtpOptionsConfiguration>();

        services.Configure<DefaultSmtpOptions>(options => _shellConfiguration.GetSection(DefaultSmtpOptions.ConfigurationSection).Bind(options));

        services.AddSettingsModel<SmtpSettings>(EmailSettings.GroupId, "SMTP", model => model
            .ValidatedBy((services, settings, errors, _) =>
            {
                if (settings.IsEnabled)
                {
                    if (string.IsNullOrWhiteSpace(settings.DefaultSender) || !services.GetRequiredService<IEmailAddressValidator>().Validate(settings.DefaultSender))
                    {
                        errors.Add(nameof(SmtpSettings.DefaultSender), "A valid default sender is required.");
                    }

                    if (string.IsNullOrWhiteSpace(settings.ConnectionKey))
                    {
                        errors.Add(nameof(SmtpSettings.ConnectionKey), "Pick the SMTP connection to send through.");
                    }
                }

                return Task.CompletedTask;
            })
            .OnSaved(async (services, settings, _) =>
            {
                services.GetRequiredService<IOptionsUpdateNotifier>().RequestUpdate<SmtpOptions>();
                await EmailProviderDefaults.UpdateDefaultProviderAsync(services, SmtpEmailProvider.TechnicalName, settings.IsEnabled);
            }));
    }
}
