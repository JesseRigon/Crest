using Crest.Workflows.Scheduling;
using Crest.Workflows.Scheduling.Quartz.Contracts;
using Crest.Workflows.Scheduling.Quartz.Handlers;
using Crest.Workflows.Scheduling.Quartz.Services;
using Crest.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Crest.Modules;

namespace Crest.Workflows.Timers.Quartz;

[Feature("Crest.Workflows.Timers.Quartz")]
public class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddSingleton<IActivityDescriptorModifier, CronActivityDescriptorModifier>()
            .AddSingleton<ICronParser, QuartzCronParser>()
            .AddScoped<QuartzWorkflowScheduler>()
            .AddScoped<IJobKeyProvider, JobKeyProvider>()
            .AddSingleton<IModularTenantEvents, RegisterJobs>();
    }
}