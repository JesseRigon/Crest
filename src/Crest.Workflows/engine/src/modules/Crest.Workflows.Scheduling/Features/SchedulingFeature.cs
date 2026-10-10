using Crest.Workflows.Common.Features;
using Crest.Workflows.Common.Multitenancy;
using Crest.Workflows.Extensions;
using Crest.Workflows.Features.Abstractions;
using Crest.Workflows.Features.Attributes;
using Crest.Workflows.Features.Services;
using Crest.Workflows.Scheduling.Bookmarks;
using Crest.Workflows.Scheduling.Handlers;
using Crest.Workflows.Scheduling.HostedServices;
using Crest.Workflows.Scheduling.Services;
using Crest.Workflows.Scheduling.TriggerPayloadValidators;
using Crest.Workflows.Management.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Workflows.Scheduling.Features;

/// <summary>
/// Provides scheduling features to the system.
/// </summary>
[DependsOn(typeof(SystemClockFeature))]
public class SchedulingFeature : FeatureBase
{
    /// <inheritdoc />
    public SchedulingFeature(IModule module) : base(module)
    {
    }
    
    /// <summary>
    /// Gets or sets the trigger scheduler.
    /// </summary>
    public Func<IServiceProvider, IWorkflowScheduler> WorkflowScheduler { get; set; } = sp => sp.GetRequiredService<DefaultWorkflowScheduler>();
    
    /// <summary>
    /// Gets or sets the CRON parser.
    /// </summary>
    public Func<IServiceProvider, ICronParser> CronParser { get; set; } = sp => sp.GetRequiredService<CronosCronParser>();

    /// <inheritdoc />
    public override void Apply()
    {
        Services
            .AddSingleton<UpdateTenantSchedules>()
            .AddSingleton<ITenantActivatedEvent>(sp => sp.GetRequiredService<UpdateTenantSchedules>())
            .AddSingleton<ITenantDeletedEvent>(sp => sp.GetRequiredService<UpdateTenantSchedules>())
            .AddSingleton<IScheduler, LocalScheduler>()
            .AddSingleton<CronosCronParser>()
            .AddSingleton(CronParser)
            .AddScoped<ITriggerScheduler, DefaultTriggerScheduler>()
            .AddScoped<IBookmarkScheduler, DefaultBookmarkScheduler>()
            .AddScoped<DefaultWorkflowScheduler>()
            .AddScoped(WorkflowScheduler)
            .AddBackgroundTask<CreateSchedulesBackgroundTask>()
            .AddHandlersFrom<ScheduleWorkflows>()

            //Trigger payload validators.
            .AddTriggerPayloadValidator<CronTriggerPayloadValidator, CronTriggerPayload>();

        Module.Configure<WorkflowManagementFeature>(management => management.AddActivitiesFrom<SchedulingFeature>());
    }
}