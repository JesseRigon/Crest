using Crest;
using Crest.Workflows.Common.Multitenancy.HostedServices;
using Crest.Workflows.Extensions;
using Crest.Workflows.Mediator.HostedServices;
using Crest.Workflows.Mediator.Options;
using Crest.Workflows.Resilience.Extensions;
using Crest.Workflows.Runtime.Distributed.Extensions;
using Medallion.Threading.FileSystem;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Crest.ContentManagement.Handlers;
using Crest.Workflows.Approvals;
using Crest.Workflows.Connectors;
using Crest.Workflows.Contexts;
using Crest.Workflows.Registry;
using Crest.Workflows.Handlers.Content;
using Crest.Workflows.Indexes;
using Crest.Workflows.Migrations;
using Crest.Workflows.Security;
using Crest.Workflows.Services;
using Crest.Workflows.StartupTasks;
using Crest.Workflows.UIHints;
using Crest.Workflows.Stores;
using Crest.Workflows.Units;
using Crest.Workflows.CommitStates;
using Crest.Workflows.Pipelines.ActivityExecution;
using Crest.Workflows.Pipelines.WorkflowExecution;
using Crest.Workflows.Runtime;
using Crest.Workflows.Mediator.Extensions;
using Microsoft.Extensions.Logging;
using Crest.Data;
using Crest.Data.Migration;
using Crest.Environment.Shell;
using Crest.Environment.Shell.Configuration;
using Crest.Modules;
using Crest.Navigation;
using Crest.Security.Permissions;

namespace Crest.Workflows.Features;

[Feature("Crest.Workflows")]
public class CoreStartup(IOptions<ShellOptions> shellOptions, ShellSettings shellSettings, IShellConfiguration shellConfiguration) : StartupBase
{
    // Last of all startups: Crest orders them by feature dependency and then stably by
    // Order, so registrations here win over any a platform module makes for the same
    // service (docs/workflows.md › Engine).
    public override int Order => int.MaxValue;

    // The pipeline is a different order. With endpoint routing, the authorization
    // middleware (Users module, default order) evaluates the matched Crest.Workflows endpoint's policy
    // when it runs, so the Crest.Workflows grant must already be on the principal by then. This
    // startup's middleware is therefore slotted ahead of the authentication order; the
    // Crest.Workflows branch authenticates the request itself (see ConfigureAsync) so the gate sees
    // the real principal, then the grant is in place before UseAuthorization.
    public override int ConfigureOrder => PlatformConstants.ConfigureOrder.Authentication - 1;

    public override void ConfigureServices(IServiceCollection services)
    {
        // One Crest.Workflows per shell. Every Crest.Workflows service, store and hosted service below lives in
        // the tenant's container; the stores use the tenant's ISession. The file lock
        // directory is the one thing Crest.Workflows would otherwise share across tenants (its
        // default is App_Data/locks relative to the process): put it under this shell's
        // own App_Data so tenants, and the dev/test instances, never lock on each other.
        var lockDirectory = Path.Combine(
            shellOptions.Value.ShellsApplicationDataPath,
            shellOptions.Value.ShellsContainerName,
            shellSettings.Name,
            "locks");

        services.AddCrestWorkflows(crestWorkflows =>
        {
            crestWorkflows.AddActivitiesFrom<CoreStartup>();
            crestWorkflows.UseWorkflowManagement(workflowManagement =>
            {
                workflowManagement.UseWorkflowDefinitionPublisher(sp => ActivatorUtilities.CreateInstance<ContentItemWorkflowDefinitionPublisher>(sp));
                workflowManagement.UseWorkflowDefinitions(workflowDefinitions => workflowDefinitions.WorkflowDefinitionStore = sp => ActivatorUtilities.CreateInstance<YesSqlWorkflowDefinitionStore>(sp));
                workflowManagement.UseWorkflowInstances(workflowInstances => workflowInstances.WorkflowInstanceStore = sp => ActivatorUtilities.CreateInstance<YesSqlWorkflowInstanceStore>(sp));
                workflowManagement.UseCache();
            });
            crestWorkflows.UseWorkflowRuntime(workflowRuntime =>
            {
                workflowRuntime.TriggerStore = sp => ActivatorUtilities.CreateInstance<YesSqlTriggerStore>(sp);
                workflowRuntime.BookmarkStore = sp => ActivatorUtilities.CreateInstance<YesSqlBookmarkStore>(sp);
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
            });
            crestWorkflows.UseScheduling();
            crestWorkflows.UseJavaScript();
            crestWorkflows.UseLiquid();
            // Connector retries ride the engine's resilience feature: the connection's policy as a strategy.
            crestWorkflows.UseResilience(resilience => resilience.AddResilienceStrategyType<ConnectionResilienceStrategy>());
            crestWorkflows.UseWorkflowsApi(api => api.AddFastEndpointsAssembly<CoreStartup>());

            // The access gate as engine middleware (docs/operations.md step 4): the engine's
            // default lists, kept by re-applying them, with the workflow gate inserted first
            // (right after the builder's Reset) and the activity gate inserted before the
            // terminal invoker. Inserted, not a rebuilt list, so an engine update that adds a
            // default middleware does not silently drop the gate. These setters replace the
            // delegate (last call wins); the AppFeature runs this configurator after the
            // engine's own feature, so this is the last call.
            crestWorkflows.UseWorkflows(workflows => workflows
                .WithDefaultWorkflowExecutionPipeline(pipeline => pipeline.Insert<WorkflowAccessGateMiddleware>(0))
                .WithDefaultActivityExecutionPipeline(pipeline => pipeline.Insert<ActivityAccessGateMiddleware>(pipeline.Components.Count() - 1)));
        });

        // Crest may dispose the tenant container synchronously; Crest.Workflows's tenant service is
        // async-only and would crash the process on that path (SyncDisposableTenantService).
        SyncDisposableTenantService.ReplaceIn(services);

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
            .AddScoped<BackgroundJobStore>()
            .AddScoped<BackgroundJobContext>()
            .AddSingleton<ShellScopedBackgroundConsumers>()
            .Configure<Crest.Workflows.Runtime.Options.StimulusSenderOptions>(options => options.QueueUnmatchedBroadcastStimuli = false)
            .AddScoped<Hooks.WorkflowHookService>()
            .AddScoped<Hooks.WorkflowHookRunner>()
            .AddScoped<IWorkflowHookRunner>(sp => sp.GetRequiredService<Hooks.WorkflowHookRunner>())
            .AddScoped<Contents.ContentFieldValueCopier>()
            .AddScoped<IWorkflowObjectLock, WorkflowObjectLock>()
            .AddScoped<Fields.WorkflowFieldDependencyAnalyzer>()
            .AddScoped<Fields.FieldDependencyChecker>()
            .AddScoped<IWorkflowFieldDependencyChecker>(sp => sp.GetRequiredService<Fields.FieldDependencyChecker>())
            .AddSingleton<IActivityDescriptorModifier, Fields.FieldDependencyDescriptorModifier>()
            .AddSingleton<IActivityDescriptorModifier, RequiredPermissionDescriptorModifier>()
            .AddScoped<WorkflowCallerResolver>()
            // The registry's query kind: one run-query descriptor per query the catalog lists.
            .AddScoped<IActivityProvider, Queries.QueryActivityProvider>()
            .AddNotificationHandler<Fields.FieldDependencyPublishHandler>()
            .Configure<Microsoft.AspNetCore.Mvc.MvcOptions>(options => options.Filters.Add<Hooks.WorkflowHookFailedExceptionFilter>())
            .AddScoped<IWorkflowHookSlotProvider, Hooks.WorkflowsHookSlotProvider>()
            .AddNotificationHandler<Hooks.HookAttachmentAtomicityValidator>();
        // Scheduled tasks run in a shell scope, not the engine's bare service scope.
        foreach (var engineHandler in services.Where(d => d.ServiceType == typeof(Crest.Workflows.Mediator.Contracts.ICommandHandler) && d.ImplementationType == typeof(Crest.Workflows.Scheduling.Handlers.RunScheduledTaskHandler)).ToList())
        {
            services.Remove(engineHandler);
        }

        services.AddScoped<Crest.Workflows.Mediator.Contracts.ICommandHandler, ShellScopedRunScheduledTaskHandler>();
        services.Replace(ServiceDescriptor.Scoped<IBackgroundActivityInvoker, ShellScopedBackgroundActivityInvoker>());
        services.Replace(ServiceDescriptor.Scoped<ICommitStateHandler>(sp => new UnitOfWorkCommitStateHandler(
            sp.GetRequiredService<DefaultCommitStateHandler>(),
            sp.GetRequiredService<WorkflowUnitOfWork>(),
            sp.GetRequiredService<Crest.Data.Documents.IDocumentStore>(),
            sp.GetRequiredService<ILogger<UnitOfWorkCommitStateHandler>>())));

        // Ownership tiers, the permission set and per-definition access (docs/workflows.md,
        // phase 5). The guard sits in the publisher and store; the access handler joins
        // Crest's authorization pipeline; the linker narrows Studio's links to what the
        // user may do, replacing the engine's static one.
        services.AddPermissionProvider<WorkflowsPermissionProvider>();
        services
            .AddScoped<WorkflowOwnershipGuard>()
            .AddScoped<WorkflowDefinitionAccessService>()
            .AddScoped<IWorkflowDefinitionAccessReader>(sp => sp.GetRequiredService<WorkflowDefinitionAccessService>())
            .AddScoped<IWorkflowDefinitionAuthorizer>(sp => sp.GetRequiredService<WorkflowDefinitionAccessService>())
            .AddScoped<Crest.Access.IAccessCeiling, WorkflowDefinitionAccessCeiling>();
        services.Replace(ServiceDescriptor.Scoped<Crest.Workflows.Api.IWorkflowDefinitionLinker, WorkflowDefinitionLinker>());

        // Connectors (docs/workflows.md, phase 3): per-shell HTTP client, limiters and token
        // cache (singletons of the tenant's container), connections in a tenant document.
        services.Configure<WorkflowConnectorOptions>(shellConfiguration.GetSection(WorkflowConnectorOptions.ConfigurationSection));
        services
            .AddSingleton<ConnectorHttpClient>()
            .AddSingleton<ConnectorRateLimiters>()
            .AddSingleton<ConnectorTokenCache>()
            .AddScoped<WorkflowConnectionService>()
            .AddScoped<ConnectorInvoker>()
            .AddScoped<ConnectorOAuthClient>()
            .AddScoped<IWorkflowActivityProvider, ConnectionOperationActivityProvider>()
            .AddScoped<Crest.Workflows.Resilience.IResilienceStrategySource, ConnectionResilienceStrategySource>()
            .AddScoped<IWorkflowConnectorProvider, GenericConnectorProvider>()
            .AddScoped<IPropertyUIHandler, ConnectionOptionsProvider>();

        // Approvals (docs/workflows.md, phase 4): a task per decision, a queue per user.
        services
            .AddDataMigration<ApprovalTaskMigrations>()
            .AddIndexProvider<ApprovalTaskIndexProvider>()
            .AddScoped<ApprovalService>()
            .AddScoped<IPropertyUIHandler, RoleOptionsProvider>();

        // The admin surface: the Workflows menu root and its page gates (the Studio pages in
        // blazor-wasm).
        services.AddNavigationProvider<Navigation.WorkflowsAdminMenu>();
        services.AddScoped<Navigation.WorkflowsRoutePermissionProvider>();
        services.AddScoped<Crest.Services.ICrestRoutePermissionProvider>(sp => sp.GetRequiredService<Navigation.WorkflowsRoutePermissionProvider>());
        services.AddScoped<Crest.Services.ICrestWebAssemblyRouteProvider>(sp => sp.GetRequiredService<Navigation.WorkflowsRoutePermissionProvider>());

        // The sample host set this at the process level; here it is per shell.
        services.Configure<MediatorOptions>(options => options.JobWorkerCount = 1);

        services.Configure<StoreCollectionOptions>(o =>
        {
            o.Collections.Add(WorkflowCollections.WorkflowInstances);
            o.Collections.Add(WorkflowCollections.StoredTriggers);
            o.Collections.Add(WorkflowCollections.StoredBookmarks);
            o.Collections.Add(WorkflowCollections.WorkflowExecutionLogRecords);
            o.Collections.Add(WorkflowCollections.ActivityExecutionRecords);
        });

        services
            .AddDataMigration<WorkflowDefinitionMigrations>()
            .AddDataMigration<WorkflowInstanceMigrations>()
            .AddDataMigration<StoredTriggerMigrations>()
            .AddDataMigration<StoredBookmarkMigrations>()
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
        routes.MapWorkflowsApi();
        return ValueTask.CompletedTask;
    }
}
