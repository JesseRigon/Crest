using Microsoft.Extensions.DependencyInjection;
using Crest.Forms.Workflows.Activities;
using Crest.Modules;
using Crest.Workflows.Platform.Helpers;

namespace Crest.Forms.Workflows;

[RequireFeatures("Crest.Workflows")]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<ValidateAntiforgeryTokenTask>();
        services.AddActivity<AddModelValidationErrorTask>();
        services.AddActivity<ValidateFormTask>();
        services.AddActivity<ValidateFormFieldTask>();
        services.AddActivity<BindModelStateTask>();
    }
}
