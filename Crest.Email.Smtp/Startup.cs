using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Crest.DisplayManagement.Handlers;
using Crest.Email.Smtp.Drivers;
using Crest.Email.Smtp.Extensions;
using Crest.Email.Smtp.Services;
using Crest.Environment.Options;
using Crest.Environment.Shell.Configuration;

namespace Crest.Email.Smtp;

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
            .AddSiteDisplayDriver<SmtpSettingsDisplayDriver>()
            .AddSignalOptionsChangeTokenSource<SmtpOptions>()
            .AddTransient<IConfigureOptions<SmtpOptions>, SmtpOptionsConfiguration>()
            .AddTransient<IPostConfigureOptions<DefaultSmtpOptions>, DefaultSmtpOptionsConfiguration>();

        services.Configure<DefaultSmtpOptions>(options =>
        {
            // To ensure backward compatibility, we will try to associate SMTP settings from multiple sections.
            // The 'Crest_Email' section will be phased out in an upcoming release.
            _shellConfiguration.GetSection("Crest_Email").Bind(options);
            _shellConfiguration.GetSection("Crest_Email_Smtp").Bind(options);

            options.IsEnabled = options.ConfigurationExists();
        });
    }
}
