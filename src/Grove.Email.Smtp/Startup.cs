using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Grove.DisplayManagement.Handlers;
using Grove.Email.Smtp.Extensions;
using Grove.Email.Smtp.Services;
using Grove.Environment.Options;
using Grove.Environment.Shell.Configuration;

namespace Grove.Email.Smtp;

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
    }
}
