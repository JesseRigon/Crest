using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Grove.Environment.Shell.Configuration;
using Grove.ReCaptcha.Configuration;

namespace Grove.ReCaptcha.Extensions;

public static class PlatformBuilderExtensions
{
    public static PlatformBuilder ConfigureReCaptchaSettings(this PlatformBuilder builder)
    {
        builder.ConfigureServices((tenantServices, serviceProvider) =>
        {
            var configurationSection = serviceProvider.GetRequiredService<IShellConfiguration>().GetSection("Grove_ReCaptcha");

            tenantServices.PostConfigure<ReCaptchaSettings>(settings => configurationSection.Bind(settings));
        });

        return builder;
    }
}
