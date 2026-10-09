using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;
using Crest.Roles.Workflows.Activities;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Roles.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<UnassignUserRoleTask>();
        services.AddActivity<GetUsersByRoleTask>();
    }
}
