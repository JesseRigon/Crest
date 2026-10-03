using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Extensions;
using Crest.Workflows.Scheduling;
using Crest.Workflows.Scheduling.Bookmarks;
using Crest.Workflows.Scheduling.Features;
using Crest.Workflows.Scheduling.Handlers;
using Crest.Workflows.Scheduling.Services;
using Crest.Workflows.Scheduling.TriggerPayloadValidators;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace Crest.Workflows.Timers.Common;

[Feature("Crest.Workflows.Timers")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.ConfigureCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.AddActivitiesFrom<SchedulingFeature>();
        });   
        
        services
            .AddSingleton<UpdateTenantSchedules>()
            .AddSingleton<ITenantActivatedEvent>(sp => sp.GetRequiredService<UpdateTenantSchedules>())
            .AddSingleton<ITenantDeletedEvent>(sp => sp.GetRequiredService<UpdateTenantSchedules>())
            .AddSingleton<IModularTenantEvents, CreateSchedules>()
            .AddSingleton<IScheduler, LocalScheduler>()
            .AddSingleton<ICronParser, CronosCronParser>()
            .AddScoped<ITriggerScheduler, DefaultTriggerScheduler>()
            .AddScoped<IBookmarkScheduler, DefaultBookmarkScheduler>()
            .AddScoped<DefaultWorkflowScheduler>()
            .AddScoped<IWorkflowScheduler, DefaultWorkflowScheduler>()
            .AddHandlersFrom<ScheduleWorkflows>()
            .AddTriggerPayloadValidator<CronTriggerPayloadValidator, CronTriggerPayload>()
            ;
    }
}