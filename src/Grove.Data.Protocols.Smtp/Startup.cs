using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.Display.Management.Handlers;
using Grove.Data.Protocols.Smtp.Extensions;
using Grove.Data.Protocols.Smtp.Services;
using Grove.Environment.Options;
using Grove.Environment.Shell.Configuration;
using Grove.Communications.Email;

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
        services.AddSmtpEmailProvider()
            .AddSignalOptionsChangeTokenSource<SmtpOptions>()
            .AddTransient<IConfigureOptions<SmtpOptions>, SmtpOptionsConfiguration>()
            .AddTransient<IPostConfigureOptions<DefaultSmtpOptions>, DefaultSmtpOptionsConfiguration>();

        services.Configure<DefaultSmtpOptions>(options =>
        {
            // To ensure backward compatibility, we will try to associate SMTP settings from multiple sections.
            // The 'Grove_Email' section will be phased out in an upcoming release.
            _shellConfiguration.GetSection("Grove_Email").Bind(options);
            _shellConfiguration.GetSection("Grove_Email_Smtp").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });

        services.AddApiContributor<Api.SmtpSettingsApi>();
    }
}
