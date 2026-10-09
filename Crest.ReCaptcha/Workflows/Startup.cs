using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Workflows.Platform.Helpers;

namespace Crest.ReCaptcha.Workflows;

[RequireFeatures("Crest.Workflows", "Crest.ReCaptcha")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ValidateReCaptchaTask>();
    }
}
