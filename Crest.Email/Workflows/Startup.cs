using Microsoft.Extensions.DependencyInjection;
using Crest.Email.Workflows.Activities;
using Crest.Modules;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Email.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<EmailTask>();
    }
}
