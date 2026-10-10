using Fluid;
using Microsoft.Extensions.DependencyInjection;
using Crest.ContentManagement;
using Crest.ContentManagement.Display.ContentDisplay;
using Crest.Data.Migration;
using Crest.Modules;

namespace Crest.ReCaptcha.Forms;

[RequireFeatures("Crest.Forms", "Crest.ReCaptcha")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<ReCaptchaPart>();
        });

        services.AddContentPart<ReCaptchaPart>()
            .UseDisplayDriver<ReCaptchaPartDisplayDriver>();

        services.AddDataMigration<Migrations>();
    }
}
