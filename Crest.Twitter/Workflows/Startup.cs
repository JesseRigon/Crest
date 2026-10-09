using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Twitter.Workflows.Activities;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Twitter.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<UpdateTwitterStatusTask>();
    }
}
