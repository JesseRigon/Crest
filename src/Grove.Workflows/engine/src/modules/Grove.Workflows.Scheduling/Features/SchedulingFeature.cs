using Grove.Workflows.Common.Features;
using Grove.Workflows.Common.Multitenancy;
using Grove.Workflows.Extensions;
using Grove.Workflows.Features.Abstractions;
using Grove.Workflows.Features.Attributes;
using Grove.Workflows.Features.Services;
using Grove.Workflows.Scheduling.Bookmarks;
using Grove.Workflows.Scheduling.Handlers;
using Grove.Workflows.Scheduling.HostedServices;
using Grove.Workflows.Scheduling.Services;
using Grove.Workflows.Scheduling.TriggerPayloadValidators;
using Grove.Workflows.Management.Features;
using Microsoft.Extensions.DependencyInjection;

namespace Grove.Workflows.Scheduling.Features;

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