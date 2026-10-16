using Grove;
using Grove.Workflows.Common.Multitenancy.HostedServices;
using Grove.Workflows.Extensions;
using Grove.Workflows.Mediator.HostedServices;
using Grove.Workflows.Mediator.Options;
using Grove.Workflows.Resilience.Extensions;
using Grove.Workflows.Runtime.Distributed.Extensions;
using Medallion.Threading.FileSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Grove.Contents.Management.Handlers;
using Grove.Workflows.Approvals;
using Grove.Workflows.Connectors;
using Grove.Workflows.Contexts;
using Grove.Workflows.Registry;
using Grove.Workflows.Handlers.Content;
using Grove.Workflows.Indexes;
using Grove.Workflows.Migrations;
using Grove.Workflows.Security;
using Grove.Workflows.Services;
using Grove.Workflows.StartupTasks;
using Grove.Workflows.UIHints;
using Grove.Workflows.Stores;
using Grove.Workflows.Units;
using Grove.Workflows.CommitStates;
using Grove.Workflows.Pipelines.ActivityExecution;
using Grove.Workflows.Pipelines.WorkflowExecution;
using Grove.Workflows.Runtime;
using Grove.Workflows.Mediator.Extensions;
using Microsoft.Extensions.Logging;
using Grove.Data;
using Grove.Data.Migration;
using Grove.Environment.Shell;
using Grove.Environment.Shell.Configuration;
using Grove.Modules;
using Grove.Display.Navigation;
using Grove.Security.Permissions;

namespace Grove.Workflows.Features;

[Feature("Grove.Workflows")]
public class CoreStartup(IOptions<ShellOptions> shellOptions, ShellSettings shellSettings, IShellConfiguration shellConfiguration) : StartupBase
{
    // Last of all startups: Grove orders them by feature dependency and then stably by
    // Order, so registrations here win over any a platform module makes for the same
    // service (docs/workflows.md › Engine).
    public override int Order => int.MaxValue;

    // The pipeline is a different order. With endpoint routing, the authorization
    // middleware (Users module, default order) evaluates the matched engine endpoint's policy
    // when it runs, so the engine grant must already be on the principal by then. This
    // startup's middleware is therefore slotted ahead of the authentication order. It does no
    // authentication of its own: the shell selector admitted the request through the access
    // gate before the tenant pipeline ran (the engine API is one of the paths it gates), so the
    // caller is already set; a request that reached here without one is refused.
    public override int ConfigureOrder => PlatformConstants.ConfigureOrder.Authentication - 1;

    public override void ConfigureServices(IServiceCollection services)
    {
        // One Grove.Workflows per shell. Every Grove.Workflows service, store and hosted service below lives in
        // the tenant's container; the stores use the tenant's ISession. The file lock
        // directory is the one thing Grove.Workflows would otherwise share across tenants (its
        // default is App_Data/locks relative to the process): put it under this shell's
        // own App_Data so tenants, and the dev/test instances, never lock on each other.
        var lockDirectory = Path.Combine(
            shellOptions.Value.ShellsApplicationDataPath,
            shellOptions.Value.ShellsContainerName,
            shellSettings.Name,
            "locks");

        services.AddGroveWorkflows(groveWorkflows =>
        {
            groveWorkflows.AddActivitiesFrom<CoreStartup>();
            groveWorkflows.UseWorkflowManagement(workflowManagement =>
            {
                workflowManagement.UseWorkflowDefinitionPublisher(sp => ActivatorUtilities.CreateInstance<ContentItemWorkflowDefinitionPublisher>(sp));
                workflowManagement.UseWorkflowDefinitions(workflowDefinitions => workflowDefinitions.WorkflowDefinitionStore = sp => ActivatorUtilities.CreateInstance<YesSqlWorkflowDefinitionStore>(sp));
                workflowManagement.UseWorkflowInstances(workflowInstances => workflowInstances.WorkflowInstanceStore = sp => ActivatorUtilities.CreateInstance<YesSqlWorkflowInstanceStore>(sp));
                workflowManagement.UseCache();
            });
            groveWorkflows.UseWorkflowRuntime(workflowRuntime =>
            {
                workflowRuntime.TriggerStore = sp => ActivatorUtilities.CreateInstance<YesSqlTriggerStore>(sp);
                workflowRuntime.BookmarkStore = sp => ActivatorUtilities.CreateInstance<YesSqlBookmarkStore>(sp);
                workflowRuntime.BookmarkQueueStore = sp => ActivatorUtilities.CreateInstance<YesSqlBookmarkQueueStore>(sp);
                workflowRuntime.WorkflowExecutionLogStore = sp => ActivatorUtilities.CreateInstance<YesSqlWorkflowExecutionLogStore>(sp);
                workflowRuntime.ActivityExecutionLogStore = sp => ActivatorUtilities.CreateInstance<YesSqlActivityExecutionRecordStore>(sp);
                workflowRuntime.DistributedLockProvider = _ =>
                {
                    Directory.CreateDirectory(lockDirectory);
                    return new FileDistributedSynchronizationProvider(new DirectoryInfo(lockDirectory));
                };
                workflowRuntime.UseDistributedRuntime();
                workflowRuntime.UseCache();
                // Background activities are durable jobs run in shell scopes after the unit
                // commits; the bookmark queue is processed in a shell scope too (Server/Units).
                workflowRuntime.BackgroundActivityScheduler = sp => ActivatorUtilities.CreateInstance<DurableBackgroundActivityScheduler>(sp);
                workflowRuntime.BookmarkQueueWorker = sp => ActivatorUtilities.CreateInstance<ShellScopedBookmarkQueueWorker>(sp);
                // Every dispatch carries the dispatcher's caller as the sealed actor (Security).
                var dispatcher = workflowRuntime.WorkflowDispatcher;
                workflowRuntime.WorkflowDispatcher = sp => ActivatorUtilities.CreateInstance<ActorStampingWorkflowDispatcher>(sp, dispatcher(sp));
            });
            groveWorkflows.UseScheduling();
            groveWorkflows.UseJavaScript();
            groveWorkflows.UseLiquid();
            // The engine's resilience feature, for the strategies a tenant picks on a node. Connector
            // activities have none by default: the connector engine retries per the connection's
            // policy, so attempts never multiply.
            groveWorkflows.UseResilience();
            groveWorkflows.UseWorkflowsApi();

            // The access gate as engine middleware (docs/operations.md step 4): the engine's
            // default lists, kept by re-applying them, with the workflow gate inserted first
            // (right after the builder's Reset) and the activity gate inserted before the
            // terminal invoker. Inserted, not a rebuilt list, so an engine update that adds a
            // default middleware does not silently drop the gate. These setters replace the
            // delegate (last call wins); the AppFeature runs this configurator after the
            // engine's own feature, so this is the last call.
            groveWorkflows.UseWorkflows(workflows => workflows
                .WithDefaultWorkflowExecutionPipeline(pipeline => pipeline.Insert<WorkflowAccessGateMiddleware>(0))
                .WithDefaultActivityExecutionPipeline(pipeline => pipeline.Insert<ActivityAccessGateMiddleware>(pipeline.Components.Count() - 1)));
        });

        // One Liquid (docs/blazor-display.md § 12): the engine's Liquid expressions render through
        // the platform's template manager; the variable types a workflow may hold are admitted
        // by the platform's member allow-list, as the engine's own context admitted them.
        services.Replace(ServiceDescriptor.Scoped<Grove.Workflows.Expressions.Liquid.Contracts.ILiquidTemplateManager, Grove.Workflows.Expressions.PlatformLiquidTemplateManager>());
        services.AddOptions<Fluid.TemplateOptions>().Configure<Microsoft.Extensions.Options.IOptions<Grove.Workflows.Management.Options.ManagementOptions>>((template, management) =>
        {
            foreach (var variable in management.Value.VariableDescriptors.Where(descriptor => descriptor.Type is { IsClass: true, ContainsGenericParameters: false }))
            {
                Fluid.MemberAccessStrategyExtensions.Register(template.MemberAccessStrategy, variable.Type);
            }
        });

        // Grove may dispose the tenant container synchronously; Grove.Workflows's tenant service is
        // async-only and would crash the process on that path (SyncDisposableTenantService).
        SyncDisposableTenantService.ReplaceIn(services);

        // The engine's management API, declared in the server's API registry (docs/MVC/api.md P7).
        services.AddApiContributor<Api.WorkflowsManagementApi>();

        // The module's own APIs under api/grove/workflows (the registry, webhooks, hooks,
        // approvals, definitions, triggers, display actions, pending state).
        services
            .AddApiContributor<Api.WorkflowRegistryApi>()
            .AddApiContributor<Api.WorkflowWebhooksApi>()
            .AddApiContributor<Api.WorkflowHooksApi>()
            .AddApiContributor<Api.WorkflowApprovalsApi>()
            .AddApiContributor<Api.WorkflowDefinitionsApi>()
            .AddApiContributor<Api.WorkflowTriggersApi>()
            .AddApiContributor<Api.WorkflowActionsApi>()
            .AddApiContributor<Api.WorkflowPendingApi>();

        // The two action pipelines (Units/ActionPipelines.cs names which class forms which).
        // Units of work (docs/workflows.md › Posting on workflows): a burst is one transaction
        // (the stores no longer commit mid-run), stimuli fire after commit, a failed unit is
        // discarded at commit and recorded in a fresh scope. External calls (connectors, the
        // stock mail/SMS/notification/HTTP tasks) are engine background activities on a durable,
        // shell-scoped scheduler: bookmarked in the unit, run after commit, the flow resumed by
        // bookmark. Hooks run attachments inside the unit; the analyzer decides what may be
        // attached.
        services
            .AddScoped<WorkflowUnitOfWork>()
            .AddScoped<WorkflowAfterCommit>()
            .AddScoped<WorkflowStimulusQueue>()
            .AddScoped<WorkflowAtomicityAnalyzer>()
            .AddDataMigration<WorkflowBackgroundJobMigrations>()
            .AddIndexProvider<WorkflowBackgroundJobIndexProvider>()
            .AddDataMigration<WorkflowStimulusMigrations>()
            .AddIndexProvider<WorkflowStimulusIndexProvider>()
            .AddScoped<BackgroundJobStore>()
            .AddScoped<BackgroundJobContext>()
            .AddSingleton<ShellScopedBackgroundConsumers>()
            .Configure<Grove.Workflows.Runtime.Options.StimulusSenderOptions>(options => options.QueueUnmatchedBroadcastStimuli = false)
            .AddScoped<Hooks.WorkflowHookService>()
            .AddScoped<Hooks.WorkflowHookRunner>()
            .AddScoped<IWorkflowHookRunner>(sp => sp.GetRequiredService<Hooks.WorkflowHookRunner>())
            .AddScoped<Contents.ContentFieldValueCopier>()
            .AddScoped<Contents.ContentOperationService>()
            .AddScoped<IWorkflowObjectLock, WorkflowObjectLock>()
            .AddScoped<Fields.WorkflowFieldDependencyAnalyzer>()
            .AddScoped<Fields.FieldDependencyChecker>()
            .AddScoped<IWorkflowFieldDependencyChecker>(sp => sp.GetRequiredService<Fields.FieldDependencyChecker>())
            .AddSingleton<IActivityDescriptorModifier, Fields.FieldDependencyDescriptorModifier>()
            .AddSingleton<IActivityDescriptorModifier, RequiredPermissionDescriptorModifier>()
            .AddScoped<WorkflowCallerResolver>()
            .AddSingleton<WorkflowActorSeal>()
            // Display trees (docs/blazor-display.md § 4a): workflow actions are decided like an
            // HTTP endpoint workflow, when the tree is built and when the action runs.
            .AddScoped<Display.WorkflowDisplayActionAuthorizer>()
            .AddScoped<Display.IUiActionContracts, Display.UiActionContracts>()
            .AddScoped<Grove.Display.Management.Components.IDisplayActionFilter, Display.WorkflowDisplayActionFilter>()
            .AddScoped<Grove.Display.Management.Components.IDisplayActionValidator, Display.WorkflowDisplayActionValidator>()
            // The registry's query kind: one run-query descriptor per query the catalog lists.
            .AddScoped<IActivityProvider, Queries.QueryActivityProvider>()
            // The registry's content operations: one descriptor per content type and operation.
            .AddScoped<IActivityProvider, Contents.ContentOperationActivityProvider>()
            .AddNotificationHandler<Fields.FieldDependencyPublishHandler>()
            .AddApiExceptionMapper<Hooks.WorkflowHookFailedExceptionMapper>()
            .AddScoped<IWorkflowHookSlotProvider, Hooks.WorkflowsHookSlotProvider>()
            .AddNotificationHandler<Hooks.HookAttachmentAtomicityValidator>();
        // Scheduled tasks run in a shell scope, not the engine's bare service scope.
        foreach (var engineHandler in services.Where(d => d.ServiceType == typeof(Grove.Workflows.Mediator.Contracts.ICommandHandler) && d.ImplementationType == typeof(Grove.Workflows.Scheduling.Handlers.RunScheduledTaskHandler)).ToList())
        {
            services.Remove(engineHandler);
        }

        services.AddScoped<Grove.Workflows.Mediator.Contracts.ICommandHandler, ShellScopedRunScheduledTaskHandler>();
        services.Replace(ServiceDescriptor.Scoped<IBackgroundActivityInvoker, ShellScopedBackgroundActivityInvoker>());
        services.Replace(ServiceDescriptor.Scoped<ICommitStateHandler>(sp => new UnitOfWorkCommitStateHandler(
            sp.GetRequiredService<DefaultCommitStateHandler>(),
            sp.GetRequiredService<WorkflowUnitOfWork>(),
            sp.GetRequiredService<Grove.Data.Documents.IDocumentStore>(),
            sp.GetRequiredService<ILogger<UnitOfWorkCommitStateHandler>>())));

        // Ownership tiers, the permission set and per-definition access (docs/workflows.md,
        // phase 5). The guard sits in the publisher and store; the access handler joins
        // Grove's authorization pipeline; the linker narrows Studio's links to what the
        // user may do, replacing the engine's static one.
        services.AddPermissionProvider<WorkflowsPermissionProvider>();
        services.AddScoped<Grove.Environment.Shell.IFeatureEventHandler, WorkflowRolesFeatureEvents>();
        services
            .AddScoped<WorkflowOwnershipGuard>()
            .AddScoped<WorkflowDefinitionAccessService>()
            .AddScoped<EngineInstanceAccess>()
            .AddScoped<IWorkflowDefinitionAccessReader>(sp => sp.GetRequiredService<WorkflowDefinitionAccessService>())
            .AddScoped<IWorkflowDefinitionAuthorizer>(sp => sp.GetRequiredService<WorkflowDefinitionAccessService>())
            .AddScoped<Grove.Access.IAccessCeiling, WorkflowDefinitionAccessCeiling>();
        services.Replace(ServiceDescriptor.Scoped<Grove.Workflows.Api.IWorkflowDefinitionLinker, WorkflowDefinitionLinker>());

        // Connector activities and the webhook trigger are consumers of the connector engine
        // (Grove.Data.Connectors, docs/connectors.md): the palette offers one preset per
        // connection operation, and connection inputs list the engine's connections.
        services
            .AddScoped<IWorkflowActivityProvider, ConnectionOperationActivityProvider>()
            .AddScoped<IPropertyUIHandler, ConnectionOptionsProvider>();

        // Approvals (docs/workflows.md, phase 4): a task per decision, a queue per user.
        services
            .AddDataMigration<ApprovalTaskMigrations>()
            .AddIndexProvider<ApprovalTaskIndexProvider>()
            .AddScoped<ApprovalService>()
            .AddScoped<IPropertyUIHandler, RoleOptionsProvider>();

        // The admin surface: the Workflows menu section (its pages, in blazor-wasm, declare
        // their own routes, permissions and menu places).
        services.AddPageSource<Navigation.WorkflowsPages>();

        // The sample host set this at the process level; here it is per shell.
        services.Configure<MediatorOptions>(options => options.JobWorkerCount = 1);

        services.Configure<StoreCollectionOptions>(o =>
        {
            o.Collections.Add(WorkflowCollections.WorkflowInstances);
            o.Collections.Add(WorkflowCollections.StoredTriggers);
            o.Collections.Add(WorkflowCollections.StoredBookmarks);
            o.Collections.Add(WorkflowCollections.BookmarkQueue);
            o.Collections.Add(WorkflowCollections.WorkflowExecutionLogRecords);
            o.Collections.Add(WorkflowCollections.ActivityExecutionRecords);
        });

        services
            .AddDataMigration<WorkflowDefinitionMigrations>()
            .AddDataMigration<WorkflowInstanceMigrations>()
            .AddDataMigration<StoredTriggerMigrations>()
            .AddDataMigration<StoredBookmarkMigrations>()
            .AddDataMigration<BookmarkQueueItemMigrations>()
            .AddDataMigration<WorkflowExecutionLogRecordMigrations>()
            .AddDataMigration<ActivityExecutionRecordMigrations>()
            .AddSingleton<JobRunnerHostedService>()
            .AddSingleton<ActivateTenants>()
            .AddScoped<IModularTenantEvents, PopulateRegistriesTask>()
            .AddScoped<IModularTenantEvents, StartHostedServices>()
            .AddScoped<IContentHandler, WorkflowDefinitionContentHandler>()
            .AddScoped<WorkflowDefinitionPartMapper>()
            .AddScoped<WorkflowDefinitionPartSerializer>()
            .AddScoped<IWorkflowUserContextAccessor, WorkflowUserContextAccessor>()
            .AddScoped<IWorkflowAuthorizer, WorkflowAuthorizer>()
            // The fifth registry: what modules contribute (triggers, flows) and the one call they make.
            .AddScoped<WorkflowRegistryCatalog>()
            .AddScoped<WorkflowFlowLookup>()
            .AddScoped<IWorkflowTriggerPublisher, WorkflowTriggerPublisher>()
            .AddScoped<IWorkflowTriggerProvider, WorkflowsTriggerProvider>()
            .AddScoped<WorkflowFlowImporter>()
            // Drop-down options for the designer's property panel.
            .AddScoped<IPropertyUIHandler, PermissionOptionsProvider>()
            .AddScoped<IPropertyUIHandler, WorkflowTriggerOptionsProvider>()
            .AddIndexProvider<WorkflowDefinitionIndexProvider>()
            .AddIndexProvider<WorkflowInstanceIndexProvider>()
            .AddIndexProvider<StoredTriggerIndexProvider>()
            .AddIndexProvider<StoredBookmarkIndexProvider>()
            .AddIndexProvider<BookmarkQueueItemIndexProvider>()
            .AddIndexProvider<WorkflowExecutionLogRecordIndexProvider>()
            .AddIndexProvider<ActivityExecutionRecordIndexProvider>();
    }

    public override ValueTask ConfigureAsync(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        // Placed before the Users module's UseAuthorization (see ConfigureOrder). The request
        // path's gate authenticated the request and built its caller before routing; this
        // branch maps the caller's permissions onto the engine's grant before the
        // authorization middleware evaluates the endpoint's policy.
        app.UseWhen(
            context => context.Request.Path.StartsWithSegments(ApiSecurityMiddleware.ApiPathPrefix),
            branch => branch.UseMiddleware<ApiSecurityMiddleware>());
        return ValueTask.CompletedTask;
    }
}
