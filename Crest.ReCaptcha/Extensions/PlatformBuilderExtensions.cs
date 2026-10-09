using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Crest.Environment.Shell.Configuration;
using Crest.ReCaptcha.Configuration;

namespace Crest.ReCaptcha.Extensions;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureReCaptchaSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Crest_ReCaptcha");

            tenantServices.PostConfigure<ReCaptchaSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
